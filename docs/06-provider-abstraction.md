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

Adding a provider requires one `IChannelProvider`, one DI registration, and one entry in the
static `ProviderCatalog`. The catalog is the single source for Admin UI choices, compatibility,
field metadata/defaults, validation, connection-test availability, and webhook relevance. It is
deliberately not a plugin framework.

## Implemented providers and configuration

| Key | Channel | Required settings | Required secrets | Optional/default fields | Safe test |
|---|---|---|---|---|---|
| `fake-sms` | SMS | — | — | `failureRatePercent=0` | yes |
| `fake-email` | Email | — | — | `failureRatePercent=0` | yes |
| `fake-whatsapp` | WhatsApp | — | — | `failureRatePercent=0` | yes |
| `twilio` | SMS | — | `accountSid`, `authToken` | `fromNumber` | yes |
| `africas-talking` | SMS | `username` | `apiKey` | `shortCode` | yes |
| `clickatell` | SMS | — | `apiKey` | — | yes |
| `smtp` | Email | `host` | — | `port=587`, `enableSsl=true`, `username`, `password` | no |
| `sendgrid` | Email | — | `apiKey` | `fromName` | yes |
| `mailgun` | Email | `domain` | `apiKey` | — | yes |
| `ses` | Email | — | `accessKeyId`, `secretAccessKey` | `region=us-east-1` | yes |
| `meta-whatsapp` | WhatsApp | `phoneNumberId` | `accessToken` | webhook secret | yes |
| `twilio-whatsapp` | WhatsApp | — | `accountSid`, `authToken` | `fromNumber` | yes |
| `infobip` | WhatsApp | `baseUrl` | `apiKey` | — | yes |

## Credentials

`ProviderConfiguration.EncryptedCredentials` holds a JSON dictionary encrypted with ASP.NET
Data Protection (purpose-scoped, shared key ring across hosts). Non-secret settings
(`SettingsJson`) stay queryable plaintext. See 10-security-review for key management.

Admin reads return only stored credential key names and a boolean indicating whether a webhook
secret exists. Secret values are never returned or pre-filled. Credential and webhook-secret
edits have explicit keep/replace/clear actions; unchanged required credentials do not need to be
entered again.

## Setup, testing, and activation

New provider configurations are saved disabled. Providers implementing `ITestableProvider` must
record a successful manual connection test before enablement. A connection-affecting change
(channel, provider, settings, credentials, or webhook secret) disables the configuration and
invalidates the previous test. Providers without a safe test—currently SMTP—require an explicit
enablement confirmation. Manual setup tests update test state but do not emit operational outage
or authentication notifications; monitoring failures may still notify.

Rate limit, retry count/delay, webhook, and failover position are advanced controls. Lower
position values are tried first; each provider exhausts its configured transient retries before
the sender advances to the next enabled provider.
