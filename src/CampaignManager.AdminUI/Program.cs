using CampaignManager.Application;
using CampaignManager.Application.Abstractions;
using CampaignManager.Infrastructure;
using CampaignManager.Infrastructure.Identity;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();

    builder.Host.UseSerilog((context, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Hangfire client only — the AdminUI issues jobs (recurring-campaign edits, cancellations)
    // but never runs a server; CampaignManager.Workers is the only host that executes them.
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

    builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();
    builder.Services
        .AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddIdentityCookies();
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });
    builder.Services.AddAuthorization();

    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();

    // Same tenant resolution as the API: organization comes from the auth cookie's claim.
    app.Use(async (context, next) =>
    {
        var orgClaim = context.User.FindFirst(JwtTokenService.OrganizationClaim)?.Value;
        if (Guid.TryParse(orgClaim, out var organizationId))
        {
            var userClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userClaim, out var userId);
            context.RequestServices.GetRequiredService<ITenantSetter>().Set(
                organizationId, userId == Guid.Empty ? null : userId,
                context.Connection.RemoteIpAddress?.ToString());
        }

        await next();
    });

    app.UseAuthorization();

    app.MapDefaultEndpoints();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CampaignManager.AdminUI terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
