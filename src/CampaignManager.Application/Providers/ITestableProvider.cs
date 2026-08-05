namespace CampaignManager.Application.Providers;

/// <summary>Optional capability: providers that can verify credentials/connectivity
/// without sending a message. The admin UI's "Test connection" uses this.</summary>
public interface ITestableProvider
{
    Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct);
}

/// <summary>Per-configuration send throttle (e.g. max messages/minute). Implementations
/// may delay the caller until capacity is available.</summary>
public interface IProviderThrottle
{
    Task WaitAsync(Guid providerConfigurationId, int? rateLimitPerMinute, CancellationToken ct);
}
