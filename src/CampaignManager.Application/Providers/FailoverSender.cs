using Microsoft.Extensions.Logging;

namespace CampaignManager.Application.Providers;

/// <summary>Attempts a send across an ordered provider list. Each provider gets
/// 1 + MaxRetries attempts (transient errors only, spaced by RetryDelaySeconds) and is
/// throttled to its configured rate limit; transient exhaustion falls through to the next
/// provider; rejections and successes stop immediately.</summary>
public sealed class FailoverSender
{
    private readonly IProviderThrottle _throttle;
    private readonly IProviderCircuitBreaker _circuitBreaker;
    private readonly ILogger<FailoverSender> _logger;

    public FailoverSender(
        IProviderThrottle throttle, IProviderCircuitBreaker circuitBreaker, ILogger<FailoverSender> logger)
    {
        _throttle = throttle;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<(SendResult Result, Guid? ProviderConfigurationId)> SendAsync(
        IReadOnlyList<ResolvedProvider> providers,
        ProviderSendRequest request,
        CancellationToken ct)
    {
        if (providers.Count == 0)
        {
            return (SendResult.TransientFailure("no_provider", "No enabled provider for this channel."), null);
        }

        SendResult lastResult = SendResult.TransientFailure("unknown", "No provider attempted.");
        Guid? lastConfigId = null;

        foreach (var resolved in providers)
        {
            lastConfigId = resolved.ProviderConfigurationId;
            var attempts = 1 + Math.Max(0, resolved.MaxRetries);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                await _throttle.WaitAsync(resolved.ProviderConfigurationId, resolved.RateLimitPerMinute, ct);

                try
                {
                    lastResult = await _circuitBreaker.ExecuteAsync(
                        resolved.ProviderConfigurationId,
                        token => resolved.Provider.SendAsync(request, resolved.Credentials, token),
                        ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {ProviderKey} threw while sending message {MessagePublicId}",
                        resolved.Provider.ProviderKey, request.MessagePublicId);
                    lastResult = SendResult.TransientFailure("provider_exception", ex.Message);
                }

                if (lastResult.Success || !lastResult.IsTransient)
                {
                    return (lastResult, lastConfigId);
                }

                if (attempt < attempts)
                {
                    _logger.LogWarning(
                        "Provider {ProviderKey} transient failure ({ErrorCode}); retry {Attempt}/{Max} in {Delay}s",
                        resolved.Provider.ProviderKey, lastResult.ErrorCode, attempt, attempts - 1,
                        resolved.RetryDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(resolved.RetryDelaySeconds), ct);
                }
            }

            _logger.LogWarning(
                "Provider {ProviderKey} exhausted ({ErrorCode}) for message {MessagePublicId}; failing over",
                resolved.Provider.ProviderKey, lastResult.ErrorCode, request.MessagePublicId);
        }

        return (lastResult, lastConfigId);
    }
}
