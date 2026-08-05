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

## Next steps by scale milestone

**~1M recipients/campaign**
- Replace chunked `AddRange` with `SqlBulkCopy` (EFCore.BulkExtensions) for recipients and
  pre-created messages (or create messages lazily inside batch jobs to halve insert volume).
- Stream CSV imports (`IAsyncEnumerable` parsing) instead of materializing request bodies;
  move import itself into a background job with a progress endpoint.
- Raise Hangfire `WorkerCount` and split `sends` queue servers from `campaigns` servers so
  orchestration is never starved.

**Sustained high send rates**
- Per-provider rate limiting (token bucket in Redis) so one tenant can't exhaust a shared
  provider account; provider-level circuit breakers (Polly) feeding `IProviderSelector` so
  an open circuit skips a provider without waiting for timeouts.
- Redis cache for provider configurations and dashboard tiles (invalidate on admin edits).

**Storage growth**
- Monthly partitioning of `Messages`/`DeliveryEvents` + sliding-window archive (see
  02-database-schema). Read replicas for reporting if the rollup job isn't sufficient.

**Beyond Hangfire**
- If job table contention becomes the bottleneck (typically well past tens of millions of
  jobs/day), swap `ICampaignDispatcher`'s implementation for RabbitMQ/Azure Service Bus with
  competing consumers; the Application layer and job contracts are unchanged by design.
