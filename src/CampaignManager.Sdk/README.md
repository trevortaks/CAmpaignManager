# CampaignManager.Sdk

Typed C# client for the CampaignManager API — create and track campaigns, manage recurring
series and templates, and handle compliance suppressions without hand-rolling HTTP calls.

> **Status**: preview (`1.0.0-preview.*`). The surface mirrors the API as of this release; no
> automated publish pipeline exists yet, so releases are cut manually via `dotnet pack`.

## Install

```bash
dotnet add package CampaignManager.Sdk
```

## Quick start — dependency injection (recommended)

```csharp
using CampaignManager.Sdk;
using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.DependencyInjection;

services.AddCampaignManagerClient(options =>
{
    options.BaseAddress = new Uri("https://campaigns.example.com/");
    options.Credential = new ApiKeyCredential("cmk_...");
    // or: options.Credential = new JwtCredential(options.BaseAddress, "admin@demo.local", "…");
});
```

Then inject `CampaignManagerClient` wherever you need it — it's registered as a typed
`HttpClient` through `IHttpClientFactory`.

## Quick start — no DI container

```csharp
using var client = new CampaignManagerClient(
    new Uri("https://campaigns.example.com/"), "cmk_...");

var created = await client.Campaigns.CreateAsync(new CreateCampaignRequest
{
    Name = "Welcome series",
    Channel = CampaignChannel.Sms,
    Sender = "ACME",
    MessageBody = "Hi {{FirstName}}, welcome!",
    Recipients = [new CampaignRecipientDto { Address = "+15551234567", Personalization = new() { ["FirstName"] = "Ada" } }]
});

var status = await client.Campaigns.GetAsync(created.CampaignId);
```

## Authentication

Two credential types, either satisfies every authenticated endpoint:

- **`ApiKeyCredential(string apiKey)`** — sends `X-Api-Key`. Best for server-to-server automation.
- **`JwtCredential(Uri baseAddress, string email, string password)`** — fetches a token from
  `POST api/auth/token`, caches it, and refreshes it automatically shortly before it expires.
  A `JwtCredential(Func<CancellationToken, Task<TokenResponse>> tokenProvider)` overload exists
  if you want to source tokens yourself (e.g. from a secrets manager) instead of embedding a
  password.

## CSV import

```csharp
var result = await client.Campaigns.ImportAsync(
    ImportCampaignRequest.FromFile("recipients.csv", "Spring sale", CampaignChannel.Sms, "ACME"));
```

The server enforces a 100MB request size limit on this endpoint.
Caller-supplied streams remain open after `ImportAsync`; the stream opened by `FromFile` is
closed automatically when the call completes.

## Method reference

| Client | Methods |
|---|---|
| `Auth` | `GetTokenAsync` |
| `Campaigns` | `CreateAsync`, `GetAsync`, `GetByTrackingIdAsync`, `CancelAsync`, `SearchAsync`, `SearchAllAsync` (auto-paging), `ImportAsync` |
| `CampaignSeries` | `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `PauseAsync`, `ResumeAsync`, `DeleteAsync` |
| `Templates` | `ListAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `PreviewAsync` |
| `Compliance` | `ListSuppressionsAsync`, `SuppressAsync`, `RemoveSuppressionAsync`, `EraseAsync` |

`api/webhooks/{providerKey}` is **not** part of this SDK — it's how providers (Twilio, Meta, …)
call *into* CampaignManager, not something a client calls.

## Errors

Every non-2xx response throws `CampaignManagerApiException` with `StatusCode`, `ProblemTitle`,
`ProblemDetail`, and (for 400 validation failures) `ValidationErrors`.

## Retries and idempotency

Retries are on by default for GET, HEAD, OPTIONS, and DELETE requests. The SDK retries network
errors, 408, 429, and 5xx responses with capped exponential backoff, honoring `Retry-After` on
429 where practical. Every attempt uses a fresh request message.

POST and PATCH operations—including campaign/series creation, imports, suppressions, and other
mutations—are attempted once by default. The server does not persist `Idempotency-Key` values, so
an automatic retry after an ambiguous failure could create a duplicate. Advanced callers may set
`CampaignManagerClientOptions.RetryNonIdempotentRequests = true`, accepting that risk, or add a
custom handler through the `IHttpClientBuilder` returned by `AddCampaignManagerClient`.

Options validate an absolute HTTP(S) base address, a positive timeout, and 0–10
retry attempts. Campaign searches validate page >= 1 and page size 1–100 before sending.

## Compatibility note

Channel properties now use `CampaignChannel` rather than free-form strings. JSON remains exactly
compatible: the wire values are `Sms`, `Email`, and `WhatsApp`. Status strings remain strings so
new server statuses can be consumed without an SDK release.
