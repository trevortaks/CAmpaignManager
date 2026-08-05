using CampaignManager.Domain.Enums;

namespace CampaignManager.Application.Providers;

/// <summary>Shared cache-key naming for cached provider configurations, so both the reader
/// (ProviderSelector) and the writers (provider admin commands) agree on the key shape.</summary>
public static class ProviderConfigCacheKeys
{
    public static string ForChannel(Guid organizationId, Channel channel) =>
        $"providers:{organizationId}:{channel}";
}
