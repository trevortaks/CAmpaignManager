# Risks & Mitigations

| # | Risk | Likelihood/Impact | Mitigation |
|---|---|---|---|
| 1 | Data Protection key loss ⇒ provider credentials unrecoverable | Low / Critical | Shared durable key ring, backups, key-encryption cert; re-entry of credentials is the only recovery — document it |
| 2 | Double-sends from at-least-once job execution | Low / Medium | Status-driven range-scoped jobs; 50-row save chunks minimize the window; pass provider idempotency keys where supported (SendGrid custom args) |
| 3 | Webhook arrives before `ProviderMessageId` is persisted | Medium / Low | Saves every 50 messages keep the window small; Phase 2: dead-letter unmatched callbacks to a table and replay on a timer |
| 4 | Provider account throttling/blacklisting under load | Medium / High | Phase 2 per-provider rate limits + circuit breakers; failover chains already spread load |
| 5 | One tenant's giant campaign starves others | Medium / Medium | Batch fan-out interleaves tenants across workers; Phase 2: per-tenant queue quotas or priority queues (`Campaign.Priority` field already exists) |
| 6 | Hangfire SQL storage contention at extreme scale | Low / Medium | Queue split, more replicas; `ICampaignDispatcher` abstraction allows broker swap without touching business logic |
| 7 | `Messages` table growth degrades queries | High (eventually) / Medium | Filtered + covering indexes now; monthly partitioning + retention archive per 02-database-schema |
| 8 | Committed dev secrets leak into production | Medium / High | CI check blocking deploy if `Jwt:SigningKey`/SA password match dev defaults; vault-injected config |
| 9 | Cancellation races (batch sends while cancel lands) | Certain / Low | Accepted semantics: in-flight messages complete; probe every 50 caps overshoot; documented in API spec |
| 10 | Template placeholder abuse (huge values, token smuggling) | Low / Low | Single-pass rendering (no recursion); size caps on personalization JSON are a cheap Phase-2 validator addition |
| 11 | Seeded demo credentials on a production DB | Low / High | Seeder is dev-only by config (`Database:MigrateOnStartup` + environment check before go-live); make seeding explicit CLI-only in Phase 2 |
| 12 | EF query-filter bypass via `IgnoreQueryFilters` misuse | Low / High | Only three deliberate uses (seeder, webhook correlation, provider selector — all reviewed); add an analyzer/code-review rule |
