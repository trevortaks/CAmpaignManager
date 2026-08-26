// Dev orchestration entry point. `dotnet run --project src/CampaignManager.AppHost` replaces the
// old "podman-compose up -d" + three separate `dotnet run` commands: it starts SQL Server and
// Redis as containers, waits for them to be ready, then launches Api, Workers and AdminUI wired
// to those containers via ConnectionStrings:Default / ConnectionStrings:Redis. The Aspire
// dashboard (URL printed on startup) gives live logs, traces and metrics for all five resources.
//
// Uses podman (this environment has no docker); set DOTNET_ASPIRE_CONTAINER_RUNTIME=podman before
// running, or export it in your shell profile.

var builder = DistributedApplication.CreateBuilder(args);

// No data volume: SQL Server bakes the SA password into the volume at first init, and Aspire
// generates a fresh random password every run, so a persisted volume would fail to log in on the
// next start. The Api migrates + reseeds demo data on every startup anyway (see Program.cs).
var sql = builder.AddSqlServer("sql")
    .AddDatabase("Default", "CampaignManager");

var redis = builder.AddRedis("Redis")
    .WithDataVolume();

var dbGate = builder.AddDbGate("dbGate")
    .WithReference(sql)
    .WaitFor(sql);

var api = builder.AddProject<Projects.CampaignManager_Api>("api")
    .WithHttpEndpoint(port: 5080, name: "http") // matches scripts/smoke.sh's default API_URL
    .WithReference(sql)
    .WithReference(redis)
    .WaitFor(sql)
    .WaitFor(redis);

builder.AddProject<Projects.CampaignManager_Workers>("workers")
    .WithReference(sql)
    .WithReference(redis)
    .WaitFor(sql)
    .WaitFor(redis)
    .WaitFor(api);

builder.AddProject<Projects.CampaignManager_AdminUI>("adminui")
    .WithReference(sql)
    .WithReference(redis)
    .WaitFor(sql)
    .WaitFor(redis)
    .WaitFor(api);

builder.Build().Run();
