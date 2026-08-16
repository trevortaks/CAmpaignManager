# Campaign Manager

Multi-tenant, provider-agnostic campaign platform (SMS / Email / WhatsApp) built on .NET 8,
Clean Architecture, EF Core + SQL Server, Hangfire, Redis, MediatR, and ASP.NET Identity.

## Quick start (local dev, zero real credentials)

```bash
# 1. SQL Server + Redis (Podman)
podman-compose up -d
# (equivalent manually: podman run for mssql per docs/09-deployment.md, plus
#  podman run -d --name campaignmanager-redis -p 6379:6379 redis:7-alpine)

# 2. Build & test
dotnet build
dotnet test

# 3. Run the API (migrates + seeds demo data on startup) and the Workers
ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/CampaignManager.Api --no-launch-profile
ASPNETCORE_URLS=http://localhost:5090 dotnet run --project src/CampaignManager.Workers --no-launch-profile
# optional admin UI
ASPNETCORE_URLS=http://localhost:5100 dotnet run --project src/CampaignManager.AdminUI --no-launch-profile

# 4. End-to-end smoke test (100-recipient + 2,100-recipient bulk-copy campaigns, recurring
#    campaign CRUD, metrics endpoint — all against fake providers)
./scripts/smoke.sh

# 5. Optional: Prometheus + Grafana dashboard over the two hosts' /metrics endpoints
podman-compose --profile observability up -d
```

Seeded dev logins: API/AdminUI `admin@demo.local` / `Admin!Passw0rd1`; API key
`cmk_dev_2f9c1a8e4b7d3f60`; webhook secret `dev-webhook-secret`.

- Swagger: http://localhost:5080/swagger
- Hangfire dashboard: http://localhost:5090/hangfire
- Admin UI: http://localhost:5100 (Dashboard, Campaigns, Recurring, Providers, Templates,
  Reports, API Keys, Users, Audit, Notifications)
- Metrics: http://localhost:5080/metrics and http://localhost:5090/metrics (Prometheus format)
- Grafana (with `--profile observability`): http://localhost:3000 (anonymous viewer)

Redis is optional — if `ConnectionStrings:Redis` is unset, caching and provider rate limiting
fall back to a single-process in-memory implementation.

## Provider setup

The Admin UI guides administrators through channel selection, a compatible provider, and typed
provider-specific fields. New real-provider configurations are disabled until their configuration
is valid and a safe connection test succeeds. SMTP has no non-sending authentication test, so
enabling it requires an explicit warning confirmation. Stored credentials and webhook secrets are
never rendered; edits choose **keep**, **replace**, or **clear**.

Implemented providers: fake SMS/Email/WhatsApp, Twilio SMS, Africa's Talking, Clickatell, SMTP,
SendGrid, Mailgun, Amazon SES, Meta WhatsApp, Twilio WhatsApp, and Infobip. Required settings and
secrets are listed in [`docs/06-provider-abstraction.md`](docs/06-provider-abstraction.md).

## Documentation

Architecture, database schema/ERD, API spec, auth flows, background processing with sequence
diagrams, provider abstraction, admin UI/dashboard/reporting design, deployment & CI/CD,
security review, scalability, risks and the phased plan live in [`docs/`](docs/).

## Solution layout

```
src/   Domain · Contracts · Application · Infrastructure · Api · Workers · AdminUI
tests/ UnitTests · IntegrationTests (real SQL, throwaway per-run database)
docs/  01–13 architecture documents
observability/  Prometheus/Grafana/OTel-collector config for the optional profile
scripts/        smoke.sh (E2E demo) · partition-messages.sql (documented, manual)
```
