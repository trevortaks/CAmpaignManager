using CampaignManager.Domain.Enums;

namespace CampaignManager.Application.Providers;

public sealed record ResolvedProvider(
    Guid ProviderConfigurationId,
    IChannelProvider Provider,
    ProviderCredentials Credentials);

/// <summary>Returns the organization's enabled providers for a channel ordered by priority
/// (lower Priority value first). Callers iterate the list to implement failover.</summary>
public interface IProviderSelector
{
    Task<IReadOnlyList<ResolvedProvider>> GetOrderedProvidersAsync(
        Guid organizationId, Channel channel, CancellationToken ct);
}
