using System.Collections.Concurrent;
using CampaignManager.Application.Providers;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace CampaignManager.Infrastructure.Providers;

/// <summary>One Polly circuit breaker per ProviderConfigurationId: after a burst of transient
/// failures the breaker opens for a cooldown, and further sends fail fast with a
/// "circuit_open" transient result so FailoverSender moves on to the next provider
/// immediately instead of waiting out a timeout against a known-down provider.</summary>
public sealed class PollyProviderCircuitBreaker : IProviderCircuitBreaker
{
    private readonly ConcurrentDictionary<Guid, ResiliencePipeline<SendResult>> _pipelines = new();
    private readonly ILogger<PollyProviderCircuitBreaker> _logger;

    public PollyProviderCircuitBreaker(ILogger<PollyProviderCircuitBreaker> logger)
    {
        _logger = logger;
    }

    public async Task<SendResult> ExecuteAsync(
        Guid providerConfigurationId, Func<CancellationToken, Task<SendResult>> action, CancellationToken ct)
    {
        var pipeline = _pipelines.GetOrAdd(providerConfigurationId, BuildPipeline);
        try
        {
            return await pipeline.ExecuteAsync(async token => await action(token), ct);
        }
        catch (BrokenCircuitException)
        {
            return SendResult.TransientFailure(
                "circuit_open", "Provider circuit breaker is open; skipping this provider for now.");
        }
    }

    private ResiliencePipeline<SendResult> BuildPipeline(Guid providerConfigurationId) =>
        new ResiliencePipelineBuilder<SendResult>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<SendResult>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Result is { Success: false, IsTransient: true }),
                OnOpened = _ =>
                {
                    _logger.LogWarning(
                        "Circuit breaker OPEN for provider configuration {ProviderConfigId}",
                        providerConfigurationId);
                    return default;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation(
                        "Circuit breaker closed for provider configuration {ProviderConfigId}",
                        providerConfigurationId);
                    return default;
                }
            })
            .Build();
}
