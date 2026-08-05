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

## Added in Phase 2

| Area | Measure |
|---|---|
| Rate limiting | ASP.NET `AddRateLimiter`: `/api/auth/token` 5/min/IP (brute-force), campaign creation 60/min per principal, webhooks 600/min per key+IP — limits configurable under `RateLimits:*` |
| Hangfire dashboard | Allow-all only in Development; other environments require HTTP Basic credentials (`HangfireDashboard:Username/Password`, constant-time compare) and refuse to start without a password |
| Webhook signatures | Meta `X-Hub-Signature-256` (HMAC-SHA256) and Twilio `X-Twilio-Signature` (HMAC-SHA1 over URL+body) verifiers, unit-tested; shared secret remains the generic fallback |
| Audit logging | `AuditSaveChangesInterceptor` records Added/Modified/Deleted for admin entities (who/what/old/new/IP) with secrets redacted; audit viewer in AdminUI |
| Callback SSRF | `SsrfGuard` resolves the callback host and rejects loopback/private/link-local/CGNAT/multicast targets (IPv4+IPv6), verified by unit tests and live; callback HttpClient disables redirects and uses a 15 s timeout |
| Credential handling in UI | Provider credentials are write-only in the admin form (stored keys listed by name; values never rendered) |
| API key lifecycle | Admin UI creation (plaintext shown once), revocation, expiry |

## Required before production (remaining gap list)

1. **Data Protection key ring**: persist to durable shared storage, encrypt keys at rest
   (certificate), and back up — key loss makes stored provider credentials unrecoverable.
2. **Secrets out of appsettings**: SA password, `Jwt:SigningKey` → environment/vault; rotate
   the dev values committed for local convenience.
3. **HTTPS enforced** at ingress + HSTS; secure/samesite cookies in AdminUI.
4. **Security headers** in AdminUI: CSP (drop the Chart.js CDN for a bundled copy), the
   frame/nosniff/referrer trio.
5. **Dependency scanning + `dotnet list package --vulnerable`** in CI.
