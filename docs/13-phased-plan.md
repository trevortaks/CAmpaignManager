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

## Phase 3 — Scale & breadth ✅ (delivered)

- **Recurring campaigns**: `CampaignSeries` template (cron expression via Cronos, own recipient
  list) + `ISeriesScheduler`/Hangfire recurring job; each occurrence materializes a full
  `Campaign` through the *same* `CreateCampaignCommand` pipeline used by the API (no duplicated
  send logic). AdminUI + API CRUD (create/edit/pause/resume/delete).
- **Remaining providers** — all seven implemented as `IChannelProvider` + `ITestableProvider`:
  SendGrid, Mailgun, Amazon SES (AWS SDK) for email; Africa's Talking, Clickatell for SMS;
  Twilio WhatsApp, Infobip for WhatsApp. **SMPP intentionally deferred** — it's a stateful,
  long-lived TCP session protocol (bind/submit_sm/enquire_link) that doesn't fit the stateless
  per-message `SendAsync` model without a dedicated session-pool manager; revisit as a Phase 4
  item alongside broker-based dispatch.
- **SqlBulkCopy + lazy message creation**: campaigns above `CreateCampaignHandler
  .BulkCopyThreshold` (2,000 recipients — tuned low for demonstrability) bulk-insert recipients
  only; `CampaignProcessingJob.DispatchAsync` bulk-creates the Queued `Message` rows lazily on
  first dispatch, halving the write volume for the common immediate-send path. Verified live
  with a 2,100-recipient campaign completing correctly via `scripts/smoke.sh`.
- **Partitioning**: documented and scripted (`scripts/partition-messages.sql`) but *not*
  auto-applied via EF migrations — rebuilding a live table's clustered index is an operational
  decision (maintenance window, backup, FK adjustments), not something to run silently on
  startup. See docs/02-database-schema.md.
- **Redis**: distributed cache (provider configs ~30s TTL, dashboard tiles ~10s TTL) and a
  cross-instance provider rate limiter (`RedisProviderThrottle`, Redis `INCR` fixed window);
  both fall back to a single-process implementation when `ConnectionStrings:Redis` is unset, so
  Redis stays optional for single-instance deployments. Per-provider **circuit breaker**
  (Polly, one breaker per `ProviderConfigurationId`) fails fast on a known-down provider instead
  of retrying into it on every message.
- **Reports**: CSV/Excel (ClosedXML)/PDF (QuestPDF) exports plus an AdminUI Reports page (date
  range, totals, top channels/campaigns, daily detail) over `DailyStatistics` — verified
  producing valid `.xlsx`/`.pdf` files live.
- **Notifications**: `INotificationService` gates on per-organization `NotificationSettings`
  (provider offline/auth-failed, campaign completed, campaign failed/high-failure-rate with a
  configurable threshold), sends via SMTP when configured, and always records a
  `NotificationLog` row (sent or not) so admins have an audit trail even without a mail relay.
- **OpenTelemetry metrics**: a custom `CampaignMetrics` meter (messages sent/failed, provider
  send duration, campaigns created/completed by status, webhooks received) instrumented
  directly in the send pipeline and webhook controller; exposed via the Prometheus exporter at
  `GET /metrics` on both Api and Workers. `podman-compose --profile observability up -d` adds
  an OTLP collector, Prometheus (scraping both hosts) and Grafana with an auto-provisioned
  datasource and starter dashboard — confirmed end-to-end (metric created → scraped → queryable
  through Grafana's Prometheus proxy).

## Phase 4 — Enterprise hardening

- External IdP (OIDC) option; fine-grained permissions.
- Broker-based dispatch (RabbitMQ/ASB) if Hangfire storage becomes the bottleneck; SMPP support
  as part of the same session-oriented rework.
- Read replicas / reporting store; multi-region strategy; attachment storage (blob/S3).
- Compliance: PII retention policies, right-to-erasure jobs, per-tenant encryption keys.
- Apply `scripts/partition-messages.sql` once volume warrants it; wire retention/archive jobs.
