using Microsoft.Extensions.Logging;

namespace CampaignManager.Application.Providers;

/// <summary>Attempts a send across an ordered provider list: transient provider failures
/// fall through to the next provider; rejections and successes stop immediately.</summary>
public sealed class FailoverSender
{
    private readonly ILogger<FailoverSender> _logger;

    public FailoverSender(ILogger<FailoverSender> logger)
    {
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
            ct.ThrowIfCancellationRequested();
            lastConfigId = resolved.ProviderConfigurationId;
            try
            {
                lastResult = await resolved.Provider.SendAsync(request, resolved.Credentials, ct);
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

            _logger.LogWarning(
                "Provider {ProviderKey} transient failure ({ErrorCode}) for message {MessagePublicId}; failing over",
                resolved.Provider.ProviderKey, lastResult.ErrorCode, request.MessagePublicId);
        }

        return (lastResult, lastConfigId);
    }
}
