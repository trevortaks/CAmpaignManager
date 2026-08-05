#!/usr/bin/env bash
# End-to-end smoke test against a running Api (port 5080) and Workers (port 5090).
# Requires: curl, jq. Uses only seeded dev data — no real provider credentials.
set -euo pipefail

API="${API_URL:-http://localhost:5080}"

echo "== 1. Health =="
curl -sf "$API/health/ready" && echo " ready"

echo "== 2. JWT token =="
TOKEN=$(curl -sf -X POST "$API/api/auth/token" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Admin!Passw0rd1"}' | jq -r .accessToken)
echo "token acquired (${#TOKEN} chars)"

echo "== 3. Unauthenticated call is rejected =="
CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API/api/campaigns")
[ "$CODE" = "401" ] && echo "401 as expected" || { echo "expected 401, got $CODE"; exit 1; }

echo "== 4. Create campaign (100 recipients, fake SMS provider) =="
RECIPIENTS=$(for i in $(seq -w 0 99); do
  printf '{"address":"+26377123400%s","personalization":{"FirstName":"User%s"}},' "$i" "$i"
done | sed 's/,$//')
CREATE=$(curl -sf -X POST "$API/api/campaigns" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"Smoke Test\",\"channel\":\"Sms\",\"sender\":\"SMOKE\",
       \"messageBody\":\"Hello {{FirstName}}, this is a smoke test.\",
       \"recipients\":[$RECIPIENTS]}")
echo "$CREATE" | jq .
CAMPAIGN_ID=$(echo "$CREATE" | jq -r .campaignId)
TRACKING_ID=$(echo "$CREATE" | jq -r .trackingId)

echo "== 5. Poll status until terminal =="
for _ in $(seq 1 30); do
  STATUS=$(curl -sf "$API/api/campaigns/$CAMPAIGN_ID" -H "Authorization: Bearer $TOKEN")
  STATE=$(echo "$STATUS" | jq -r .status)
  echo "  status=$STATE sent=$(echo "$STATUS" | jq .statistics.sent) failed=$(echo "$STATUS" | jq .statistics.failed)"
  case "$STATE" in Completed|CompletedWithErrors|Failed) break;; esac
  sleep 3
done
echo "$STATUS" | jq .

echo "== 6. Lookup by tracking id =="
curl -sf "$API/api/campaigns/by-tracking/$TRACKING_ID" -H "Authorization: Bearer $TOKEN" | jq -r .status

echo "== 7. Webhook flips a sent message to Delivered =="
# The fake provider logs providerMessageId; use API key auth + search to fetch one is not exposed,
# so ask the DB via the campaign status delta instead: pick a providerMessageId from Workers logs,
# or pass one via env WEBHOOK_PMID.
if [ -n "${WEBHOOK_PMID:-}" ]; then
  curl -sf -X POST "$API/api/webhooks/fake-sms" \
    -H "Content-Type: application/json" -H "X-Webhook-Secret: dev-webhook-secret" \
    -d "{\"providerMessageId\":\"$WEBHOOK_PMID\",\"status\":\"Delivered\"}" -o /dev/null -w "%{http_code}\n"
fi

echo "== 8. API key auth works =="
curl -sf "$API/api/campaigns?pageSize=5" -H "X-Api-Key: cmk_dev_2f9c1a8e4b7d3f60" | jq '.totalCount'

echo "== 9. Scheduled campaign + cancel =="
SCHED=$(curl -sf -X POST "$API/api/campaigns" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Scheduled Smoke","channel":"Sms","sender":"SMOKE",
       "messageBody":"later","scheduledAtUtc":"2030-01-01T00:00:00Z",
       "recipients":[{"address":"+263771234599"}]}')
SCHED_ID=$(echo "$SCHED" | jq -r .campaignId)
echo "scheduled: $(echo "$SCHED" | jq -r .status)"
curl -sf -X POST "$API/api/campaigns/$SCHED_ID/cancel" -H "Authorization: Bearer $TOKEN" -o /dev/null -w "cancel: %{http_code}\n"
curl -sf "$API/api/campaigns/$SCHED_ID" -H "Authorization: Bearer $TOKEN" | jq -r .status

echo "== 10. Recurring campaign: create, verify next run, pause, delete =="
SERIES=$(curl -sf -X POST "$API/api/campaign-series" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Smoke Recurring","channel":"Sms","sender":"SMOKE","messageBody":"Hi {{FirstName}}",
       "cronExpression":"0 8 * * *","isActive":true,
       "recipients":[{"address":"+263771000401","personalization":{"FirstName":"Ada"}}]}')
SERIES_ID=$(echo "$SERIES" | jq -r .seriesId)
curl -sf "$API/api/campaign-series" -H "Authorization: Bearer $TOKEN" \
  | jq --arg id "$SERIES_ID" '.[] | select(.id == $id) | {name, isActive, nextRunAtUtc}'
curl -sf -X POST "$API/api/campaign-series/$SERIES_ID/pause" -H "Authorization: Bearer $TOKEN" \
  -o /dev/null -w "pause: %{http_code}\n"
curl -sf -X DELETE "$API/api/campaign-series/$SERIES_ID" -H "Authorization: Bearer $TOKEN" \
  -o /dev/null -w "delete: %{http_code}\n"

echo "== 11. Bulk-copy path: campaign above BulkCopyThreshold (2,000 recipients) =="
BULK_RECIPIENTS=$(for i in $(seq 0 2099); do printf '{"address":"+2637800%05d"},' "$((10#$i))"; done | sed 's/,$//')
BULK_CREATE=$(curl -sf -X POST "$API/api/campaigns" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"Bulk Copy Smoke\",\"channel\":\"Sms\",\"sender\":\"SMOKE\",\"messageBody\":\"bulk\",\"recipients\":[$BULK_RECIPIENTS]}")
BULK_ID=$(echo "$BULK_CREATE" | jq -r .campaignId)
echo "$BULK_CREATE" | jq '{campaignId, status}'
for _ in $(seq 1 40); do
  BULK_STATUS=$(curl -sf "$API/api/campaigns/$BULK_ID" -H "Authorization: Bearer $TOKEN")
  BULK_STATE=$(echo "$BULK_STATUS" | jq -r .status)
  case "$BULK_STATE" in Completed|CompletedWithErrors|Failed) break;; esac
  sleep 3
done
echo "$BULK_STATUS" | jq '{status, statistics}'
[ "$(echo "$BULK_STATUS" | jq -r .statistics.total)" = "2100" ] && echo "bulk-copy total recipients OK (2100)"

echo "== 12. Metrics endpoint exposes custom counters =="
curl -sf "$API/metrics" | grep -q campaignmanager_campaigns_created && echo "Api /metrics OK"

echo "SMOKE TEST PASSED"
