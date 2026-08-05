using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Providers.Registry;

public sealed class ProviderSelector : IProviderSelector
{
    private readonly IAppDbContext _db;
    private readonly IProviderRegistry _registry;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<ProviderSelector> _logger;

    public ProviderSelector(
        IAppDbContext db,
        IProviderRegistry registry,
        ICredentialProtector protector,
        ILogger<ProviderSelector> logger)
    {
        _db = db;
        _registry = registry;
        _protector = protector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResolvedProvider>> GetOrderedProvidersAsync(
        Guid organizationId, Channel channel, CancellationToken ct)
    {
        var configs = await _db.ProviderConfigurations
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId && p.Channel == channel && p.IsEnabled)
            .OrderBy(p => p.Priority)
            .AsNoTracking()
            .ToListAsync(ct);

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
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(config.SettingsJson) ?? [];

            resolved.Add(new ResolvedProvider(config.Id, provider, new ProviderCredentials(secrets, settings)));
        }

        return resolved;
    }
}
