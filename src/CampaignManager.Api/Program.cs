using CampaignManager.Api.Middleware;
using CampaignManager.Application;
using CampaignManager.Infrastructure;
using CampaignManager.Infrastructure.Identity;
using CampaignManager.Infrastructure.Persistence;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Hangfire client only — the Workers host runs the server that executes jobs.
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            builder.Configuration.GetConnectionString("Default"),
            new SqlServerStorageOptions
            {
                SchemaName = builder.Configuration["Hangfire:SchemaName"] ?? "hangfire",
                PrepareSchemaIfNecessary = true
            }));

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
    builder.Services.AddSingleton<JwtTokenService>();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = JwtTokenService.ValidationParameters(jwtOptions);
        })
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.Scheme, _ => { });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("ApiAccess", policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationOptions.Scheme)
            .RequireAuthenticatedUser());
    });

    builder.Services.AddControllers();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Brute-force protection on token issuance, per client IP.
        options.AddPolicy("token", context =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimits:TokenPerMinute", 5),
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Campaign submission per authenticated principal (API key id or user id).
        options.AddPolicy("campaigns", context =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimits:CampaignsPerMinute", 60),
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Webhook flood absorption per provider key + source IP.
        options.AddPolicy("webhooks", context =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                $"{context.Request.Path}|{context.Connection.RemoteIpAddress}",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimits:WebhooksPerMinute", 600),
                    Window = TimeSpan.FromMinutes(1)
                }));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Campaign Manager API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT from POST /api/auth/token"
        });
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationOptions.HeaderName
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = Array.Empty<string>(),
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            }] = Array.Empty<string>()
        });
    });

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("Default")!,
            name: "sqlserver",
            tags: ["ready"]);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("CampaignManager.Api"))
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddHttpClientInstrumentation();
            if (builder.Configuration.GetValue<bool>("OpenTelemetry:ConsoleExporter"))
            {
                tracing.AddConsoleExporter();
            }

            if (builder.Configuration["OpenTelemetry:OtlpEndpoint"] is { Length: > 0 } otlpEndpoint)
            {
                tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            }
        })
        .WithMetrics(metrics =>
        {
            metrics.AddMeter(CampaignManager.Application.Observability.CampaignMetrics.MeterName);
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddHttpClientInstrumentation();
            metrics.AddRuntimeInstrumentation();
            // Scrape target for Prometheus (see podman-compose.yml --profile observability).
            metrics.AddPrometheusExporter();
            if (builder.Configuration.GetValue<bool>("OpenTelemetry:ConsoleExporter"))
            {
                metrics.AddConsoleExporter();
            }

            if (builder.Configuration["OpenTelemetry:OtlpEndpoint"] is { Length: > 0 } otlpEndpoint)
            {
                metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            }
        });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseAuthentication();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapPrometheusScrapingEndpoint(); // GET /metrics

    // Apply migrations and seed development data on startup.
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CampaignManager.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
