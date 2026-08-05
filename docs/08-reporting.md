# Dashboard & Reporting Design

## Dashboard (Phase 2)

Top row — stat tiles (hero numbers, no chart chrome):
**Campaigns today · Messages sent today · Failed · Pending · Queue size · Active providers**

Second row:
- **Campaign trend** — line chart, campaigns/day over 30 days.
- **Delivery vs failure rate** — two small multiples (never dual-axis), % over time.
- **Messages by channel** — bar chart (Sms/Email/WhatsApp).

Third row:
- **Messages by provider** — horizontal bar.
- **Provider health** — status list (good/warning/critical, icon + label, never color alone).
- **Recent + scheduled campaigns** — tables with status badges.

All time-series read from pre-aggregated data (see below), never `COUNT(*)` over `Messages`.

## Reports (Phase 2)

| Report | Source |
|---|---|
| Daily / monthly campaign volumes | `DailyStatistics` rollup |
| Success & failure rate | rollup (delivered ÷ sent, failed ÷ total) |
| Average delivery time | avg(`DeliveredAtUtc` − `SentAtUtc`) per day, computed in the rollup job |
| Messages by provider / channel | rollup grouped columns |
| Top campaigns / top users | rollup + campaigns join |
| Failed campaigns | campaigns where status ∈ (Failed, CompletedWithErrors) |
| Delivery timeline (per campaign) | `DeliveryEvents` for the campaign (already indexed) |

**Aggregation strategy**: a nightly (and hourly-incremental) Hangfire recurring job writes
`DailyStatistics(OrganizationId, Date, Channel, ProviderConfigurationId, Sent, Delivered,
Failed, AvgDeliverySeconds, …)`. Dashboards and reports query only this narrow table; the
`Messages` table is touched solely by per-campaign detail views (which use `(CampaignId,
Status)`).

**Exports**: CSV natively; Excel via ClosedXML; PDF via QuestPDF — all streamed
server-side from the same query objects, capped + paged to protect memory.
