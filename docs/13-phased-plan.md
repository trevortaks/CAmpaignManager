# Phased Implementation Plan

## Phase 1 — Core vertical slice ✅ (this delivery)

Solution scaffold (Clean Architecture, CPM), domain + state machines, EF Core schema +
migrations + seeder, SQL Server via Podman, JWT + API-key auth with multi-tenancy, campaign
create/status/cancel/search APIs, provider strategy with priority failover (fakes + Twilio/
SMTP/Meta stubs), Hangfire dispatch→batch→finalize pipeline, generic delivery webhooks,
template rendering, AdminUI shell (login, campaign list/detail + chart), unit + integration
tests, smoke script, architecture docs.

## Phase 2 — Operability & administration ✅ (delivered)

- Provider CRUD UI with write-only credential entry, **test connection** (`ITestableProvider`
  on all providers incl. Twilio/Meta account probes), per-provider rate limits
  (`ProviderThrottle` sliding window) and retry policies (per-provider attempts + delay in
  `FailoverSender`); last-test health shown in list + dashboard.
- Template CRUD + live preview (AdminUI page and `POST /api/templates/preview`);
  streamed CSV recipient import (`POST /api/campaigns/import`) with validation report
  (accepted/invalid/duplicates + per-line errors).
- Audit logging via `AuditSaveChangesInterceptor` (secrets redacted) + audit viewer with
  old/new diff.
- API rate limiting (token/campaigns/webhooks policies); Hangfire dashboard Basic auth
  outside Development; Meta + Twilio webhook signature verification.
- Webhook dead-letter table + 5-minute replay job (abandons after 10 attempts);
  stuck-campaign sweep (10-minute safety-net finalizer) — adapted from the original
  "requeue transient failures" idea since `Failed` is a terminal message state.
- Campaign completion callbacks with SSRF guard (private/loopback/CGNAT blocked) and
  Hangfire backoff retries (1 m→2 h).
- Hourly `DailyStatistics` rollup; admin dashboard with stat tiles, 14-day sent/failed
  trends, channel/provider breakdowns, provider health, recent + scheduled campaigns.
- API key management UI (plaintext once, revoke, expiry); user management (create, roles
  Admin/Operator/Viewer, lockout status).

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
