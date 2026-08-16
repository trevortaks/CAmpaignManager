using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Domain.Entities;
using CampaignManager.Application.Providers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Admin.Providers;

public sealed record ListProvidersQuery : IRequest<IReadOnlyList<ProviderSummary>>;

public sealed class ListProvidersHandler : IRequestHandler<ListProvidersQuery, IReadOnlyList<ProviderSummary>>
{
    private readonly IAppDbContext _db;

    public ListProvidersHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProviderSummary>> Handle(ListProvidersQuery query, CancellationToken ct)
    {
        var providers = await _db.ProviderConfigurations.AsNoTracking()
            .OrderBy(p => p.Channel).ThenBy(p => p.Priority)
            .Select(p => new
            {
                p.Id, ChannelName = p.Channel.ToString(), p.ProviderKey, p.Name, p.Priority, p.IsEnabled,
                p.RateLimitPerMinute, p.MaxRetries, p.RetryDelaySeconds,
                p.LastTestedAtUtc, p.LastTestSucceeded, p.LastTestError, p.Channel
            })
            .ToListAsync(ct);

        return providers.Select(p => new ProviderSummary(
            p.Id, p.ChannelName, p.ProviderKey, p.Name, p.Priority, p.IsEnabled,
            p.RateLimitPerMinute, p.MaxRetries, p.RetryDelaySeconds,
            p.LastTestedAtUtc, p.LastTestSucceeded, p.LastTestError,
            ProviderCatalog.Find(p.Channel, p.ProviderKey)?.SupportsConnectionTest == true)).ToList();
    }
}

public sealed record GetProviderQuery(Guid ProviderId) : IRequest<ProviderDetail>;

public sealed class GetProviderHandler : IRequestHandler<GetProviderQuery, ProviderDetail>
{
    private readonly IAppDbContext _db;
    private readonly ICredentialProtector _protector;

    public GetProviderHandler(IAppDbContext db, ICredentialProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<ProviderDetail> Handle(GetProviderQuery query, CancellationToken ct)
    {
        var config = await _db.ProviderConfigurations.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == query.ProviderId, ct)
            ?? throw new NotFoundException(nameof(ProviderConfiguration), query.ProviderId);

        var settings = string.IsNullOrEmpty(config.SettingsJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(config.SettingsJson) ?? [];
        var credentialKeys = config.EncryptedCredentials is { Length: > 0 }
            ? _protector.Unprotect(config.EncryptedCredentials).Keys.ToList()
            : [];

        return new ProviderDetail(
            config.Id, config.Channel.ToString(), config.ProviderKey, config.Name,
            config.Priority, config.IsEnabled, config.RateLimitPerMinute,
            config.MaxRetries, config.RetryDelaySeconds, settings, credentialKeys,
            !string.IsNullOrEmpty(config.WebhookSecret), config.LastTestedAtUtc, config.LastTestSucceeded);
    }
}
