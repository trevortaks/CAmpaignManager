using System.Security.Cryptography;
using System.Text;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Admin.ApiKeys;

public static class ApiKeyGenerator
{
    public const string Prefix = "cmk_";

    /// <summary>Generates a new key. The plaintext is returned exactly once.</summary>
    public static (string Plaintext, string KeyPrefix, string Hash) Generate()
    {
        var plaintext = Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        return (plaintext, plaintext[..12], Hash(plaintext));
    }

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}

public sealed record ApiKeySummary(
    Guid Id, string Name, string KeyPrefix, DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc, DateTime? RevokedAtUtc, DateTime? LastUsedAtUtc);

public sealed record CreateApiKeyCommand(string Name, DateTime? ExpiresAtUtc)
    : IRequest<(Guid Id, string Plaintext)>;

public sealed class CreateApiKeyHandler : IRequestHandler<CreateApiKeyCommand, (Guid Id, string Plaintext)>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CreateApiKeyHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<(Guid Id, string Plaintext)> Handle(CreateApiKeyCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId ?? throw new DomainException("No organization context.");
        var (plaintext, prefix, hash) = ApiKeyGenerator.Generate();
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = command.Name,
            KeyHash = hash,
            KeyPrefix = prefix,
            ExpiresAtUtc = command.ExpiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync(ct);
        return (key.Id, plaintext);
    }
}

public sealed record RevokeApiKeyCommand(Guid KeyId) : IRequest;

public sealed class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RevokeApiKeyHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task Handle(RevokeApiKeyCommand command, CancellationToken ct)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(
                k => k.Id == command.KeyId && k.OrganizationId == _tenant.OrganizationId, ct)
            ?? throw new NotFoundException(nameof(ApiKey), command.KeyId);
        key.RevokedAtUtc ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record ListApiKeysQuery : IRequest<IReadOnlyList<ApiKeySummary>>;

public sealed class ListApiKeysHandler : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeySummary>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ListApiKeysHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ApiKeySummary>> Handle(ListApiKeysQuery query, CancellationToken ct) =>
        await _db.ApiKeys.AsNoTracking()
            .Where(k => k.OrganizationId == _tenant.OrganizationId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .Select(k => new ApiKeySummary(
                k.Id, k.Name, k.KeyPrefix, k.CreatedAtUtc, k.ExpiresAtUtc, k.RevokedAtUtc, k.LastUsedAtUtc))
            .ToListAsync(ct);
}
