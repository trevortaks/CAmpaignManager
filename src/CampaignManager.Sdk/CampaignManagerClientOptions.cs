using CampaignManager.Sdk.Auth;

namespace CampaignManager.Sdk;

/// <summary>Configuration for <see cref="CampaignManagerClient(CampaignManagerClientOptions)"/>
/// (the non-DI construction path) and for <c>AddCampaignManagerClient</c>. <see cref="BaseAddress"/>
/// and <see cref="Credential"/> are required — <see cref="Validate"/> throws if either is missing.</summary>
public sealed class CampaignManagerClientOptions
{
    /// <summary>Root address of the CampaignManager API, e.g. <c>https://api.example.com/</c>.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>How requests are authenticated — an <see cref="ApiKeyCredential"/> or <see cref="JwtCredential"/>.</summary>
    public ICampaignManagerCredential? Credential { get; set; }

    /// <summary>Per-request timeout. Defaults to <see cref="HttpClient"/>'s own default (100s).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Whether transient failures (5xx, network errors, 429) are retried with exponential
    /// backoff. Defaults to true. Non-idempotent POST/PATCH calls are excluded unless explicitly
    /// enabled with <see cref="RetryNonIdempotentRequests"/>.</summary>
    public bool EnableRetries { get; set; } = true;

    /// <summary>Maximum retry attempts when <see cref="EnableRetries"/> is true.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Explicit opt-in to retry POST/PATCH requests. Disabled by default because those
    /// operations can create duplicate mutations when the server accepted a request but its
    /// response was lost.</summary>
    public bool RetryNonIdempotentRequests { get; set; }

    /// <summary>Base delay for exponential retry backoff. Set to zero in tests or when a custom
    /// outer resilience handler supplies delays.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Caps SDK-selected and Retry-After delays. Defaults to 30 seconds.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Advanced/testability hook: overrides the innermost transport (e.g. to point at an
    /// in-memory <c>WebApplicationFactory</c> test server, or a corporate proxy). Only used by the
    /// non-DI construction path.</summary>
    public HttpMessageHandler? PrimaryHandler { get; set; }

    /// <summary>Throws <see cref="InvalidOperationException"/> if a required option was not set.</summary>
    public void Validate()
    {
        if (BaseAddress is null)
        {
            throw new InvalidOperationException($"{nameof(CampaignManagerClientOptions)}.{nameof(BaseAddress)} must be set.");
        }

        if (Credential is null)
        {
            throw new InvalidOperationException($"{nameof(CampaignManagerClientOptions)}.{nameof(Credential)} must be set.");
        }

        if (!BaseAddress.IsAbsoluteUri ||
            (BaseAddress.Scheme != Uri.UriSchemeHttp && BaseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{nameof(CampaignManagerClientOptions)}.{nameof(BaseAddress)} must be an absolute HTTP or HTTPS URI.");
        }

        if (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new InvalidOperationException($"{nameof(CampaignManagerClientOptions)}.{nameof(Timeout)} must be positive and no more than {int.MaxValue} milliseconds.");
        }

        if (MaxRetryAttempts is < 0 or > 10)
        {
            throw new InvalidOperationException($"{nameof(CampaignManagerClientOptions)}.{nameof(MaxRetryAttempts)} must be between 0 and 10.");
        }

        if (RetryBaseDelay < TimeSpan.Zero || MaxRetryDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Retry delays must be non-negative and the maximum must be positive.");
        }
    }
}
