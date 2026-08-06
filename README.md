# Campaign Manager

Multi-tenant, provider-agnostic campaign platform (SMS / Email / WhatsApp) built on .NET 8,
Clean Architecture, EF Core + SQL Server, Hangfire, Redis, MediatR, and ASP.NET Identity. Local
dev is orchestrated with [.NET Aspire](https://aspire.dev) (AppHost requires the .NET 10 SDK —
app projects stay on .NET 8, see [Solution layout](#solution-layout)).

## Quick start (local dev, zero real credentials)

```bash
# 1. One-time per machine: trust the ASP.NET Core dev cert (the Aspire dashboard needs it for
#    its internal gRPC channel — on Linux this also needs SSL_CERT_DIR, which the AppHost's
#    launchSettings.json already sets for you).
dotnet dev-certs https --trust

# 2. Podman only (no Docker here): start its API socket once per session.
#    DOTNET_ASPIRE_CONTAINER_RUNTIME=podman is already baked into the AppHost's launchSettings.
systemctl --user start podman.socket

# 3. Build & test
dotnet build
dotnet test

# 4. Run everything — SQL Server, Redis, Api, Workers, AdminUI — from one AppHost.
#    Prints the Aspire dashboard URL (live logs/traces/metrics for all five resources) and
#    waits for SQL/Redis before starting the three app hosts. Ctrl+C tears down the containers.
dotnet run --project src/CampaignManager.AppHost

# 5. End-to-end smoke test (100-recipient + 2,100-recipient bulk-copy campaigns, recurring
#    campaign CRUD, metrics endpoint — all against fake providers)
./scripts/smoke.sh

# 6. Optional: Prometheus + Grafana dashboard over the two hosts' /metrics endpoints
#    (separate from the Aspire dashboard above — closer to the prod observability stack)
podman-compose --profile observability up -d
```

Seeded dev logins: API/AdminUI `admin@demo.local` / `Admin!Passw0rd1`; API key
`cmk_dev_2f9c1a8e4b7d3f60`; webhook secret `dev-webhook-secret`.

- Swagger: http://localhost:5080/swagger
- Hangfire dashboard: http://localhost:5274/hangfire
- Admin UI: http://localhost:5105 (Dashboard, Campaigns, Recurring, Providers, Templates,
  Reports, API Keys, Users, Audit, Notifications)
- Metrics: http://localhost:5080/metrics and http://localhost:5274/metrics (Prometheus format)
- Grafana (with `--profile observability`): http://localhost:3000 (anonymous viewer)

The Api's port is pinned to 5080 (matching `scripts/smoke.sh`'s default `API_URL`); Workers and
AdminUI use whatever their `launchSettings.json` HTTP profile says — check the Aspire dashboard's
resource list if you've changed those. SQL Server and Redis get fresh, randomly-assigned host
ports and generated passwords each run — the Api migrates + reseeds demo data on every startup,
so there's nothing to persist. Redis is optional even so — if `ConnectionStrings:Redis` is unset,
caching and provider rate limiting fall back to a single-process in-memory implementation.

Prefer the old manual flow (no AppHost)? `podman-compose up -d` still starts SQL Server + Redis
standalone, then run each host individually, e.g.
`ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/CampaignManager.Api --no-launch-profile`.

## Documentation

Architecture, database schema/ERD, API spec, auth flows, background processing with sequence
diagrams, provider abstraction, admin UI/dashboard/reporting design, deployment & CI/CD,
security review, scalability, risks and the phased plan live in [`docs/`](docs/).

## Solution layout

```
src/   Domain · Contracts · Application · Infrastructure · Api · Workers · AdminUI
       AppHost (net10.0, dev orchestration only) · ServiceDefaults (OTel/health/resilience, net8.0)
tests/ UnitTests · IntegrationTests (real SQL, throwaway per-run database)
docs/  01–13 architecture documents
observability/  Prometheus/Grafana/OTel-collector config for the optional profile
scripts/        smoke.sh (E2E demo) · partition-messages.sql (documented, manual)
```

`CampaignManager.AppHost` is the only project that needs the .NET 10 SDK — it's dev/orchestration
tooling, not shipped. Every other project, including `ServiceDefaults`, stays on .NET 8 and is
unaffected. `global.json` pins the repo to the .NET 10 SDK so `dotnet build`/`dotnet test` at the
root can build the AppHost too; install it alongside .NET 8 (they coexist fine) if you don't have
it yet.
