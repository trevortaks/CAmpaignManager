using CampaignManager.Domain.Enums;

namespace CampaignManager.Application.Providers;

/// <summary>Indexes all registered IChannelProvider implementations by (Channel, ProviderKey).</summary>
public interface IProviderRegistry
{
    IChannelProvider Resolve(Channel channel, string providerKey);
    bool TryResolve(Channel channel, string providerKey, out IChannelProvider? provider);
}
