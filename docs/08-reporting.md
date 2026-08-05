# Dashboard & Reporting Design

## Dashboard ✅ (Phase 2, delivered)

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

## Reports ✅ (Phase 3, delivered)

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

**Exports**: CSV natively; Excel via ClosedXML; PDF via QuestPDF — all generated server-side
from the same `GetReportQuery` result in the AdminUI `ReportsController` (`ExportCsv` /
`ExportExcel` / `ExportPdf`), verified producing valid `.xlsx`/`.pdf` files.

**Metrics** (Phase 3): a custom OpenTelemetry meter (`CampaignMetrics`) instruments the send
pipeline and webhook controller directly — messages sent/failed and provider send duration by
channel, campaigns created/completed by status, webhooks received by provider — exposed via
`GET /metrics` (Prometheus format) on both Api and Workers. See docs/09-deployment.md for the
optional Prometheus/Grafana profile.
