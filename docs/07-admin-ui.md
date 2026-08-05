# Admin UI

ASP.NET Core MVC + Bootstrap + Chart.js. Cookie auth (Identity), anti-forgery on all POSTs,
same tenant scoping as the API via the cookie's `org` claim.

## Built in Phase 1

| Page | Route | Contents |
|---|---|---|
| Sign in | `/Account/Login` | Email/password, lockout on failures |
| Campaigns | `/Campaigns` | Search box, status/channel filters, paged table |
| Campaign detail | `/Campaigns/Details/{id}` | Status badge, timings, message-status donut (Chart.js) with legend + counts table, top failure reasons |

## Full page list (Phases 2–3)

- **Dashboard** `/` — see 08-reporting.md for tile/chart design.
- **Providers** `/Providers` — list per channel with priority + enabled toggle;
  `/Providers/Edit/{id}` — credentials (write-only fields), settings, webhook secret,
  **Test connection** button, rate limits, retry policy.
- **Templates** `/Templates` — CRUD + live preview pane rendering `{{Placeholders}}`
  against sample data.
- **Recipients** — CSV upload with column mapping, validation report (invalid/duplicate),
  saved lists.
- **Reports** `/Reports` — daily/monthly volumes, success/failure rates, per-provider and
  per-channel breakdowns; export Excel/CSV/PDF.
- **API keys** `/ApiKeys` — create (plaintext shown once), revoke, expiry.
- **Users & roles** `/Users` — invite, role assignment (Admin/Operator/Viewer).
- **Audit log** `/Audit` — who/what/when/IP with before/after diff viewer.
- **Notifications** `/Notifications` — provider-offline and failure-rate alert settings.

## Chart conventions

Status colors come from the validated reference palette (dataviz method): identity is never
color-alone — every chart ships a legend and an adjacent counts table; segments are separated
by a 2px surface-colored border.
