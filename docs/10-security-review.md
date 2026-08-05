# Security Review

## Implemented in Phase 1

| Area | Measure |
|---|---|
| Credentials at rest | Provider secrets encrypted via ASP.NET Data Protection (purpose-scoped); never returned by any API |
| API keys | SHA-256 hashed + prefix lookup; constant-time comparison; expiry/revocation; plaintext shown once |
| Webhooks | Per-configuration shared secret, constant-time compare; monotonic status updates prevent spoofed downgrades |
| Tenant isolation | `OrganizationId` on every row + EF global query filters bound to the authenticated principal; jobs receive tenant explicitly |
| Input validation | FluentValidation on all commands; enum/URL/size checks; recipient cap (100k) prevents payload abuse |
| Error handling | ProblemDetails everywhere; 500s never leak exception details; validation errors are field-scoped |
| CSRF | Anti-forgery tokens on all AdminUI POST forms; API is token-based (no ambient credentials) |
| Template injection | Single-pass rendering — substituted values are never re-expanded |
| SQL injection | EF Core parameterization throughout; no raw SQL |
| Auth separation | JWT-only in Api (`AddIdentityCore`), cookies confined to AdminUI |
| Password policy | Identity defaults + 10-char minimum, lockout on failed sign-ins |

## Required before production (gap list)

1. **Data Protection key ring**: persist to durable shared storage, encrypt keys at rest
   (certificate), and back up — key loss makes stored provider credentials unrecoverable.
2. **Secrets out of appsettings**: SA password, `Jwt:SigningKey` → environment/vault; rotate
   the dev values committed for local convenience.
3. **Rate limiting**: ASP.NET `AddRateLimiter` — per-API-key fixed window on campaign
   creation, tighter window on `/api/auth/token` (brute-force) and webhooks (flooding).
4. **Hangfire dashboard**: replace the dev allow-all filter with an authenticated admin-role
   filter (it can trigger/delete jobs).
5. **HTTPS enforced** at ingress + HSTS; secure/samesite cookies in AdminUI.
6. **Webhook hardening**: where providers sign callbacks (Twilio `X-Twilio-Signature`, Meta
   HMAC), verify signatures instead of the generic shared secret.
7. **Audit logging**: populate `AuditLogs` from an EF SaveChanges interceptor for admin
   mutations (who/what/old/new/IP).
8. **Callback URL SSRF**: when campaign-completion callbacks are implemented, validate the
   URL against private-address ranges and use a no-redirect HttpClient.
9. **Security headers** in AdminUI: CSP (drop the Chart.js CDN for a bundled copy), the
   frame/nosniff/referrer trio.
10. **Dependency scanning + `dotnet list package --vulnerable`** in CI.
