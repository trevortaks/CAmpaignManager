# Scalability Recommendations

Targets: millions of recipients per campaign, thousands of campaigns, horizontal scaling.

## What Phase 1 already does right

- Stateless Api → replicate freely.
- Fan-out into 500-message batch jobs → send throughput scales with Workers replicas ×
  WorkerCount; batches are range-scoped so replicas never contend on rows.
- bigint clustered PKs on hot tables (append-only inserts), narrow `Messages` rows, no
  rendered-body storage.
- Chunked inserts (1k) with a single round-trip per chunk; keyset-friendly indexes.
- Denormalized campaign counters written once by the finalizer — dashboards never scan
  `Messages`.
- Async everywhere; 202-and-return on campaign submission.

## Delivered in Phase 3

- **`SqlBulkCopy` for large campaigns**: `CreateCampaignHandler.BulkCopyThreshold` (2,000
  recipients) routes bigger imports through `SqlBulkRecipientWriter` — a single bulk-copy
  round trip for recipients, with `Message` rows created **lazily** by
  `CampaignProcessingJob.DispatchAsync` on first dispatch, halving the write volume for the
  common immediate-send path. Verified at 2,100 recipients via `scripts/smoke.sh`.
- **Streamed CSV import** (`POST /api/campaigns/import`): parses line-by-line, never buffers
  the whole file, reports invalid/duplicate rows without failing the batch.
- **Redis-backed provider rate limiting** (`RedisProviderThrottle`, fixed-window `INCR`) —
  correct across multiple Workers replicas, unlike the single-process fallback used when Redis
  isn't configured.
- **Provider circuit breaker** (Polly, per `ProviderConfigurationId`) — an open breaker fails
  fast with `circuit_open` instead of waiting out a timeout against a known-down provider on
  every message, feeding `FailoverSender`'s existing failover-to-next-provider path.
- **Redis cache** for provider configurations (~30s TTL, invalidated on admin edits) and
  dashboard tiles (~10s TTL) — removes the DB round trip from the hot send path and from
  repeated dashboard refreshes.
- **Partitioning script** (`scripts/partition-messages.sql`) for monthly partitioning of
  `Messages`/`DeliveryEvents` — written and documented, deliberately **not** run automatically
  (see docs/02-database-schema.md for why).

## Next steps by scale milestone

**~1M recipients/campaign**
- Raise Hangfire `WorkerCount` and split `sends` queue servers from `campaigns` servers so
  orchestration is never starved.
- Move CSV import fully out-of-band (background job + progress endpoint) once files approach
  the low hundreds of thousands of rows.

**Storage growth**
- Apply `scripts/partition-messages.sql` during a maintenance window once `Messages` approaches
  the range where index maintenance/backup time becomes noticeable (rough guide: tens of
  millions of rows) — see the script for the FK/rebuild caveats. Read replicas for reporting if
  the hourly rollup job isn't sufficient.

**Beyond Hangfire**
- If job table contention becomes the bottleneck (typically well past tens of millions of
  jobs/day), swap `ICampaignDispatcher`'s implementation for RabbitMQ/Azure Service Bus with
  competing consumers; the Application layer and job contracts are unchanged by design. SMPP
  provider support (stateful session protocol) fits naturally into this same rework.
