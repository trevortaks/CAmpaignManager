# Campaign Manager

Multi-tenant, provider-agnostic campaign platform (SMS / Email / WhatsApp) built on .NET 8,
Clean Architecture, EF Core + SQL Server, Hangfire, MediatR, and ASP.NET Identity.

## Quick start (local dev, zero real credentials)

```bash
# 1. SQL Server (Podman; ~2 GB RAM)
podman run -d --name campaignmanager-sql -e ACCEPT_EULA=Y \
  -e "MSSQL_SA_PASSWORD=CampaignDev!Passw0rd" -e MSSQL_PID=Developer \
  -p 1433:1433 -v mssql-data:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest
# (or: podman-compose up -d)

# 2. Build & test
dotnet build
dotnet test

# 3. Run the API (migrates + seeds demo data on startup) and the Workers
ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/CampaignManager.Api --no-launch-profile
ASPNETCORE_URLS=http://localhost:5090 dotnet run --project src/CampaignManager.Workers --no-launch-profile
# optional admin UI
ASPNETCORE_URLS=http://localhost:5100 dotnet run --project src/CampaignManager.AdminUI --no-launch-profile

# 4. End-to-end smoke test (creates a 100-recipient campaign against fake providers)
./scripts/smoke.sh
```

Seeded dev logins: API/AdminUI `admin@demo.local` / `Admin!Passw0rd1`; API key
`cmk_dev_2f9c1a8e4b7d3f60`; webhook secret `dev-webhook-secret`.

- Swagger: http://localhost:5080/swagger
- Hangfire dashboard: http://localhost:5090/hangfire
- Admin UI: http://localhost:5100

## Documentation

Architecture, database schema/ERD, API spec, auth flows, background processing with sequence
diagrams, provider abstraction, admin UI/dashboard/reporting design, deployment & CI/CD,
security review, scalability, risks and the phased plan live in [`docs/`](docs/).

## Solution layout

```
src/   Domain · Contracts · Application · Infrastructure · Api · Workers · AdminUI
tests/ UnitTests · IntegrationTests (real SQL, throwaway per-run database)
docs/  01–13 architecture documents
```
