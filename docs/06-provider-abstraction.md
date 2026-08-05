# Provider Abstraction

Business logic never references a concrete provider. The contract lives in
`Application/Providers`:

```csharp
public interface IChannelProvider
{
    Channel Channel { get; }          // Sms | Email | WhatsApp
    string ProviderKey { get; }       // "twilio", "smtp", "meta-whatsapp", "fake-sms", …
    Task<SendResult> SendAsync(ProviderSendRequest request,
                               ProviderCredentials credentials,   // decrypted per-tenant
                               CancellationToken ct);
}

public sealed record SendResult(bool Success, string? ProviderMessageId,
                                string? ErrorCode, string? ErrorMessage, bool IsTransient);
```

`SendResult` factories encode the three outcomes that drive control flow:
`Ok(providerMessageId)` · `TransientFailure(code, msg)` (connectivity/auth/5xx → failover +
retry eligible) · `Rejected(code, msg)` (invalid recipient → terminal, no failover).

## Selection & failover

1. `IProviderSelector.GetOrderedProvidersAsync(orgId, channel)` loads the organization's
   **enabled** `ProviderConfigurations` for the channel ordered by `Priority` (lower first),
   decrypts credentials, and resolves each `ProviderKey` through `IProviderRegistry`
   (a DI-built index over every registered `IChannelProvider`).
2. `FailoverSender` walks the ordered list per message: success or rejection stops; transient
   failure (including thrown exceptions) logs and falls through to the next provider.
3. The winning configuration's id is stamped on the message for reporting.

Adding a provider = one class implementing `IChannelProvider` + one DI registration + an admin
configuration row. Nothing else changes.

## Implemented providers (Phase 1)

| Key | Channel | Notes |
|---|---|---|
| `fake-sms` / `fake-email` / `fake-whatsapp` | all | Dev/demo: logs sends, configurable `failureRatePercent`, emits `fake-…` provider message ids so webhooks are exercisable with zero credentials |
| `twilio` | SMS | REST `Messages.json`, basic auth (`accountSid`/`authToken`), `fromNumber` setting |
| `smtp` | Email | `System.Net.Mail`; host/port/ssl settings, optional credentials |
| `meta-whatsapp` | WhatsApp | Graph API v19 text messages; `accessToken` secret, `phoneNumberId` setting |

Planned (same contract): Africa's Talking, Clickatell, SMPP (SMS); SendGrid, Mailgun, SES
(email); Twilio WhatsApp, Infobip (WhatsApp).

## Credentials

`ProviderConfiguration.EncryptedCredentials` holds a JSON dictionary encrypted with ASP.NET
Data Protection (purpose-scoped, shared key ring across hosts). Non-secret settings
(`SettingsJson`) stay queryable plaintext. See 10-security-review for key management.

## Connection testing (Phase 2)

`IChannelProvider` gains `TestAsync(credentials)`; the AdminUI provider form calls it before
saving. Rate limits and per-provider retry policies also attach to `ProviderConfiguration`.
