# Admin UI

ASP.NET Core MVC + Bootstrap 5.3 (vendored locally) + Chart.js. Cookie auth (Identity),
anti-forgery on all POSTs, same tenant scoping as the API via the cookie's `org` claim.

## Shell (Phase 4 redesign)

The generic scaffold navbar was replaced with a proper admin-dashboard shell:

- **Sidebar** (`Views/Shared/_Sidebar.cshtml`) — grouped navigation (Monitor / Configure /
  Insights / Administration), active-link highlighting from route data, a footer with the
  theme toggle, signed-in user, and sign-out. Built as a Bootstrap `offcanvas-lg` element:
  in-flow and always visible at ≥992px, a slide-in drawer below that (toggled by the topbar's
  hamburger button). **Important implementation note**: the markup uses only the
  `offcanvas-lg offcanvas-start` classes — deliberately *not* the bare `.offcanvas` class.
  Bootstrap's compiled CSS gives plain `.offcanvas` an unconditional `visibility: hidden` with
  no breakpoint scoping, so combining it with `offcanvas-lg` (as Bootstrap's own docs example
  appears to suggest) leaves the sidebar permanently hidden at desktop widths unless JS adds
  `.show`. Omitting the bare class fixes this; the mobile drawer behavior is fully governed by
  `offcanvas-lg`'s own breakpoint-scoped rules, which don't depend on the bare class.
- **Topbar** (`Views/Shared/_Topbar.cshtml`) — mobile hamburger + current section name.
- **Design tokens** (`wwwroot/css/tokens.css`) — light/dark custom properties taken verbatim
  from the dataviz skill's validated reference palette, the same source the two Chart.js
  views already drew their colors from. `theme.css` remaps Bootstrap's own `--bs-*` variables
  to these tokens, so existing components (`.card`, `.table`, `.badge`, `.form-control`,
  `.pagination`, `.alert`, `.dropdown-menu`) re-theme with **no markup changes** — verified
  live across every distinct component pattern in the app (stat tiles, tables, badges, forms
  of every input type, breadcrumbs, alerts, charts).
- **Icons** — a hand-authored inline SVG sprite (`wwwroot/icons/sprite.svg`, ~14 symbols),
  not a vendored icon-font package, to avoid pulling far more than the small fixed set the
  shell actually needs.
- **Dark mode** — explicit toggle (not OS `prefers-color-scheme`), persisted to
  `localStorage`, applied pre-paint by a blocking inline script in `_Layout.cshtml`'s `<head>`
  to avoid a flash of the wrong theme on reload. `wwwroot/js/theme-toggle.js` dispatches a
  `themechange` event that the two chart-bearing views listen for.

## Page list

| Page | Route | Contents |
|---|---|---|
| Sign in | `/Account/Login` | Email/password, lockout on failures |
| Dashboard | `/` | Stat tiles, 14-day sent/failed trend, channel/provider breakdown, provider health, recent/scheduled campaigns |
| Campaigns | `/Campaigns` | Search box, status/channel filters, paged table |
| Campaign detail | `/Campaigns/Details/{id}` | Status badge, timings, message-status donut (Chart.js) with legend + counts table, top failure reasons |
| Recurring | `/CampaignSeries` | Cron-scheduled campaign templates: create/edit/pause/resume/delete |
| Providers | `/Providers` | List per channel with priority + enabled toggle; `/Providers/Edit/{id}` — credentials (write-only fields), settings, webhook secret, **Test connection** button, rate limits, retry policy |
| Templates | `/Templates` | CRUD + live preview pane rendering `{{Placeholders}}` against sample data |
| Reports | `/Reports` | Daily/monthly volumes, success/failure rates, per-provider and per-channel breakdowns; export Excel/CSV/PDF |
| API keys | `/ApiKeys` | Create (plaintext shown once), revoke, expiry |
| Users | `/Users` | Create, role assignment (Admin/Operator/Viewer) |
| Audit log | `/Audit` | Who/what/when/IP with old/new diff, filterable by entity/action |
| Notifications | `/Notifications` | Per-org alert settings + notification history log |

## Chart conventions

Status colors come from the validated reference palette (dataviz method): identity is never
color-alone — every chart ships a legend and an adjacent counts table; segments are separated
by a 2px surface-colored border. Colors are resolved client-side from CSS custom properties
(`chartColors()` helper in each view's script) so the same code renders correctly in both
themes; the message-status donut's 8-segment palette additionally keys off
`document.documentElement.dataset.theme` since those colors are per-segment identity, not the
3 categorical series slots.

**Chart.js gotcha fixed in the redesign**: `maintainAspectRatio: false` sizes the canvas to
fill its parent. Without an explicit, non-content-driven height on that parent, this creates a
runaway feedback loop — the canvas grows to fill the parent, which grows because its height is
driven by the canvas, which grows again, indefinitely. Every canvas is now wrapped in a
`.chart-container` div with a fixed pixel height (`admin.css`) to give Chart.js a stable box to
measure against. Chart instances are also kept in named variables (not discarded) specifically
so `themechange` can `.destroy()` them before rebuilding — recreating a Chart on a canvas that
already has one attached, without destroying the old instance first, is the standard Chart.js
memory-leak pattern; the `themechange` listener is registered once per page load (not inside
the render function) so toggling repeatedly never accumulates duplicate listeners or charts.
