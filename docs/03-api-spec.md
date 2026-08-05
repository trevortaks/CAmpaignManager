# API Specification

Base URL: `/api`. Auth: `Authorization: Bearer <jwt>` **or** `X-Api-Key: <key>` (policy
`ApiAccess`). Errors are RFC 7807 ProblemDetails. Swagger UI at `/swagger`.

## Auth

### POST /api/auth/token  (anonymous)
```json
{ "email": "admin@demo.local", "password": "…" }
```
→ `200 { "accessToken": "…", "expiresAtUtc": "…" }` · `401` on bad credentials.

## Campaigns

### POST /api/campaigns
Submit a campaign. Validation is synchronous; sending is asynchronous (202).

```json
{
  "name": "August Promo",
  "channel": "Sms | Email | WhatsApp",
  "sender": "PROMO",
  "subject": "only for Email",
  "messageBody": "Hello {{FirstName}}",   // or templateId
  "templateId": null,
  "scheduledAtUtc": "2026-08-10T08:00:00Z",  // optional; future ⇒ Scheduled
  "priority": 0,
  "tags": ["promo"],
  "metadata": {"source": "crm"},
  "callbackUrl": "https://caller.example/campaign-done",
  "recipients": [
    { "address": "+263771234567", "personalization": { "FirstName": "Ada" } }
  ]
}
```
→ `202 { "campaignId": "…", "trackingId": "CMP-…", "status": "Queued|Scheduled" }`
· `400` validation (max 100 000 recipients/request; duplicates deduped case-insensitively)
· `404` unknown template.

### GET /api/campaigns/{campaignId}
### GET /api/campaigns/by-tracking/{trackingId}
→ `200`:
```json
{
  "campaignId": "…", "trackingId": "CMP-…", "name": "…", "channel": "Sms",
  "status": "Draft|Scheduled|Queued|Processing|Completed|CompletedWithErrors|Failed|Cancelled",
  "scheduledAtUtc": null, "startedAtUtc": "…", "completedAtUtc": "…",
  "statistics": { "total": 100, "queued": 0, "processing": 0, "sent": 94,
                  "delivered": 1, "read": 0, "failed": 6, "rejected": 0, "expired": 0 },
  "failureReasons": [ { "error": "simulated_failure: …", "count": 6 } ]
}
```

### POST /api/campaigns/{campaignId}/cancel
→ `204` · `404` · `409` if already terminal. Deletes the scheduled job (if any), expires all
still-queued messages; in-flight batches stop at their next cancellation probe.

### GET /api/campaigns?search=&status=&channel=&fromUtc=&toUtc=&page=1&pageSize=20
`search` matches name (contains) or tracking id (exact).
→ `200 PagedResult<CampaignSummaryResponse>`.

## Webhooks

### POST /api/webhooks/{providerKey}  (anonymous + `X-Webhook-Secret`)
```json
{ "providerMessageId": "SM…", "status": "Delivered|Read|Failed|Expired|Sent|Unknown",
  "detail": "optional", "occurredAtUtc": "optional" }
```
→ `204` applied (or ignored as an out-of-order downgrade) · `401` bad/missing secret
· `404` unknown provider message id · `400` unknown status. Status updates are monotonic.

## Health

`GET /health/live` (process up) · `GET /health/ready` (SQL reachable).
