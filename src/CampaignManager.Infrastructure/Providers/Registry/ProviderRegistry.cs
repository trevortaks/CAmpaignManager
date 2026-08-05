using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Registry;

public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<(Channel, string), IChannelProvider> _providers;

    public ProviderRegistry(IEnumerable<IChannelProvider> providers)
    {
        _providers = providers.ToDictionary(p => (p.Channel, p.ProviderKey));
    }

    public IChannelProvider Resolve(Channel channel, string providerKey) =>
        _providers.TryGetValue((channel, providerKey), out var provider)
            ? provider
            : throw new InvalidOperationException(
                $"No provider registered for channel {channel} with key '{providerKey}'.");

    public bool TryResolve(Channel channel, string providerKey, out IChannelProvider? provider)
    {
        var found = _providers.TryGetValue((channel, providerKey), out var p);
        provider = p;
        return found;
    }
}
