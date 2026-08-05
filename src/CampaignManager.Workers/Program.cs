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
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        // Development-only: open dashboard. Replace with a real authorization filter
        // before any non-local deployment.
        Authorization = [new AllowAllDashboardAuthorizationFilter()],
        DisplayStorageConnectionString = false
    });

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
