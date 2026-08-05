# Phased Implementation Plan

## Phase 1 — Core vertical slice ✅ (this delivery)

Solution scaffold (Clean Architecture, CPM), domain + state machines, EF Core schema +
migrations + seeder, SQL Server via Podman, JWT + API-key auth with multi-tenancy, campaign
create/status/cancel/search APIs, provider strategy with priority failover (fakes + Twilio/
SMTP/Meta stubs), Hangfire dispatch→batch→finalize pipeline, generic delivery webhooks,
template rendering, AdminUI shell (login, campaign list/detail + chart), unit + integration
tests, smoke script, architecture docs.

## Phase 2 — Operability & administration (next)

- Provider CRUD UI with credential entry, **test connection**, per-provider rate limits and
  retry policies; provider health tracking.
- Template CRUD + preview API/UI; recipient CSV upload (streamed) with validation report.
- Audit logging via SaveChanges interceptor + audit viewer.
- API rate limiting; Hangfire dashboard auth; webhook signature verification (Twilio/Meta).
- Dead-letter table for unmatched webhooks + replay job; requeue-transient-failures sweep.
- Campaign completion callbacks (`CallbackUrl`) with SSRF guards and retry.
- `DailyStatistics` rollup job; dashboard page with tiles and trend charts.
- API key management UI; user/role management.

## Phase 3 — Scale & breadth

- Recurring campaigns (RRULE-style schedule on campaign + Hangfire recurring jobs).
- Remaining providers: Africa's Talking, Clickatell, SMPP; SendGrid/Mailgun/SES; Twilio
  WhatsApp, Infobip.
- `SqlBulkCopy` imports; lazy message creation; messages/delivery-events partitioning +
  retention archive.
- Redis caching (provider configs, dashboards) + per-provider token-bucket rate limiting and
  circuit breakers.
- Reports with Excel/CSV/PDF export; notifications (provider offline, auth failure, failure-
  rate threshold, campaign completed/failed) via email/webhook.
- OpenTelemetry metrics + OTLP export to a collector; Grafana dashboards.

## Phase 4 — Enterprise hardening

- External IdP (OIDC) option; fine-grained permissions.
- Broker-based dispatch (RabbitMQ/ASB) if Hangfire storage becomes the bottleneck.
- Read replicas / reporting store; multi-region strategy; attachment storage (blob/S3).
- Compliance: PII retention policies, right-to-erasure jobs, per-tenant encryption keys.
