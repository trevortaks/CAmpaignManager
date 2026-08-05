using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Providers.Registry;

public sealed class ProviderSelector : IProviderSelector
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IAppDbContext _db;
    private readonly IProviderRegistry _registry;
    private readonly ICredentialProtector _protector;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProviderSelector> _logger;

    public ProviderSelector(
        IAppDbContext db,
        IProviderRegistry registry,
        ICredentialProtector protector,
        IDistributedCache cache,
        ILogger<ProviderSelector> logger)
    {
        _db = db;
        _registry = registry;
        _protector = protector;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResolvedProvider>> GetOrderedProvidersAsync(
        Guid organizationId, Channel channel, CancellationToken ct)
    {
        var configs = await GetConfigsAsync(organizationId, channel, ct);

        var resolved = new List<ResolvedProvider>(configs.Count);
        foreach (var config in configs)
        {
            if (!_registry.TryResolve(channel, config.ProviderKey, out var provider) || provider is null)
            {
                _logger.LogWarning(
                    "Provider configuration {ConfigId} references unregistered provider key '{Key}'; skipping",
                    config.Id, config.ProviderKey);
                continue;
            }

            var secrets = config.EncryptedCredentials is { Length: > 0 }
                ? _protector.Unprotect(config.EncryptedCredentials)
                : new Dictionary<string, string>();
            var settings = string.IsNullOrEmpty(config.SettingsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(config.SettingsJson) ?? [];

            resolved.Add(new ResolvedProvider(
                config.Id, provider, new ProviderCredentials(secrets, settings),
                config.MaxRetries, config.RetryDelaySeconds, config.RateLimitPerMinute));
        }

        return resolved;
    }

    /// <summary>Cache-aside over the enabled provider configurations for a channel. Only
    /// non-secret shape is cached (still-encrypted credential bytes); a ~30s staleness window
    /// after an admin edit is an accepted trade-off (see docs/11-scalability.md).</summary>
    private async Task<IReadOnlyList<CachedConfig>> GetConfigsAsync(
        Guid organizationId, Channel channel, CancellationToken ct)
    {
        var key = ProviderConfigCacheKeys.ForChannel(organizationId, channel);
        var cached = await _cache.GetStringAsync(key, ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<CachedConfig>>(cached) ?? [];
        }

        var configs = await _db.ProviderConfigurations
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId && p.Channel == channel && p.IsEnabled)
            .OrderBy(p => p.Priority)
            .AsNoTracking()
            .Select(p => new CachedConfig(
                p.Id, p.ProviderKey, p.EncryptedCredentials, p.SettingsJson,
                p.MaxRetries, p.RetryDelaySeconds, p.RateLimitPerMinute))
            .ToListAsync(ct);

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(configs),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        return configs;
    }

    private sealed record CachedConfig(
        Guid Id, string ProviderKey, byte[]? EncryptedCredentials, string? SettingsJson,
        int MaxRetries, int RetryDelaySeconds, int? RateLimitPerMinute);
}
