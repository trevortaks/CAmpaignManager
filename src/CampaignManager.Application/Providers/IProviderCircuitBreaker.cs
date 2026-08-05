namespace CampaignManager.Application.Providers;

/// <summary>Per-provider-configuration circuit breaker: after a burst of transient failures,
/// short-circuits further attempts against that provider for a cooldown period so a known-down
/// provider is skipped instantly instead of retried on every message.</summary>
public interface IProviderCircuitBreaker
{
    Task<SendResult> ExecuteAsync(
        Guid providerConfigurationId, Func<CancellationToken, Task<SendResult>> action, CancellationToken ct);
}
