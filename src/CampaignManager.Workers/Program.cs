using CampaignManager.Application;
using CampaignManager.Infrastructure;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
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

    builder.Services.AddHangfireServer(options =>
    {
        options.Queues = ["campaigns", "sends", "default"];
        options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 8);
    });

    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("Default")!, name: "sqlserver");

    var app = builder.Build();

    app.MapHealthChecks("/health");
    IDashboardAuthorizationFilter dashboardAuth = app.Environment.IsDevelopment()
        ? new AllowAllDashboardAuthorizationFilter()
        : new BasicAuthDashboardAuthorizationFilter(
            app.Configuration["HangfireDashboard:Username"] ?? "admin",
            app.Configuration["HangfireDashboard:Password"]
                ?? throw new InvalidOperationException(
                    "HangfireDashboard:Password must be configured outside Development."));
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [dashboardAuth],
        DisplayStorageConnectionString = false
    });

    using (var scope = app.Services.CreateScope())
    {
        var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurring.AddOrUpdate<CampaignManager.Application.Jobs.IMaintenanceJobs>(
            "webhook-deadletter-replay", j => j.ReplayWebhookDeadLettersAsync(), "*/5 * * * *");
        recurring.AddOrUpdate<CampaignManager.Application.Jobs.IMaintenanceJobs>(
            "stuck-campaign-sweep", j => j.SweepStuckCampaignsAsync(), "*/10 * * * *");
        recurring.AddOrUpdate<CampaignManager.Application.Jobs.IMaintenanceJobs>(
            "daily-statistics-rollup", j => j.RollupDailyStatisticsAsync(), "0 * * * *");
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CampaignManager.Workers terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

internal sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}

/// <summary>HTTP Basic auth for the Hangfire dashboard outside Development.</summary>
internal sealed class BasicAuthDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public BasicAuthDashboardAuthorizationFilter(string username, string password)
    {
        _username = username;
        _password = password;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var header = httpContext.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(header["Basic ".Length..]));
                var separator = decoded.IndexOf(':');
                if (separator > 0)
                {
                    var userOk = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(decoded[..separator]),
                        System.Text.Encoding.UTF8.GetBytes(_username));
                    var passOk = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(decoded[(separator + 1)..]),
                        System.Text.Encoding.UTF8.GetBytes(_password));
                    if (userOk && passOk) return true;
                }
            }
            catch (FormatException)
            {
            }
        }

        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return false;
    }
}
