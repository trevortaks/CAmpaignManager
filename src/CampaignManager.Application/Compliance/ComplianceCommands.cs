using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Compliance;

// Two of Phase 4's "Compliance" bullet, and the only sub-items that are a real obligation
// today rather than scale-triggered infra (broker dispatch, read replicas, multi-region,
// SMPP, partitioning all wait for an actual bottleneck per docs/13-phased-plan.md):
//   - suppression list, so an opt-out (STOP/unsubscribe/complaint) is never messaged again
//   - right-to-erasure, so a deletion request actually removes stored PII
// Per-tenant encryption keys and a scheduled retention/purge job are skipped here — no
// concrete retention period or KMS target has been specified, so building either now would
// be speculative; add when a real policy names a duration or a key-management provider.

public sealed record SuppressionSummary(Guid Id, string Address, string? Channel, string Reason, DateTime CreatedAtUtc);

public sealed record AddSuppressionCommand(string Address, string? Channel, string Reason) : IRequest<Guid>;

public sealed class AddSuppressionHandler : IRequestHandler<AddSuppressionCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public AddSuppressionHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(AddSuppressionCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId ?? throw new DomainException("No organization context.");
        Channel? channel = null;
        if (!string.IsNullOrWhiteSpace(command.Channel))
        {
            if (!Enum.TryParse<Channel>(command.Channel, ignoreCase: true, out var parsed))
            {
                throw new DomainException("Channel must be one of: Sms, Email, WhatsApp.");
            }
            channel = parsed;
        }

        var address = command.Address.Trim().ToLowerInvariant();
        var existing = await _db.Suppressions.FirstOrDefaultAsync(
            s => s.Address == address && s.Channel == channel, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var suppression = new Suppression
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Address = address,
            Channel = channel,
            Reason = string.IsNullOrWhiteSpace(command.Reason) ? "opt-out" : command.Reason,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Suppressions.Add(suppression);
        await _db.SaveChangesAsync(ct);
        return suppression.Id;
    }
}

public sealed record DeleteSuppressionCommand(Guid Id) : IRequest;

public sealed class DeleteSuppressionHandler : IRequestHandler<DeleteSuppressionCommand>
{
    private readonly IAppDbContext _db;

    public DeleteSuppressionHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteSuppressionCommand command, CancellationToken ct)
    {
        var suppression = await _db.Suppressions.FirstOrDefaultAsync(s => s.Id == command.Id, ct)
            ?? throw new NotFoundException(nameof(Suppression), command.Id);
        _db.Suppressions.Remove(suppression);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record ListSuppressionsQuery : IRequest<IReadOnlyList<SuppressionSummary>>;

public sealed class ListSuppressionsHandler : IRequestHandler<ListSuppressionsQuery, IReadOnlyList<SuppressionSummary>>
{
    private readonly IAppDbContext _db;

    public ListSuppressionsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SuppressionSummary>> Handle(ListSuppressionsQuery query, CancellationToken ct) =>
        await _db.Suppressions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SuppressionSummary(s.Id, s.Address, s.Channel == null ? null : s.Channel.ToString(), s.Reason, s.CreatedAtUtc))
            .ToListAsync(ct);
}

/// <summary>Redacts an address's PII from every campaign recipient list in the tenant
/// (Message keeps only a foreign key + status, so it carries no PII of its own).</summary>
public sealed record EraseRecipientCommand(string Address) : IRequest<int>;

public sealed class EraseRecipientHandler : IRequestHandler<EraseRecipientCommand, int>
{
    private const string RedactedMarker = "[erased]";
    private readonly IAppDbContext _db;

    public EraseRecipientHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(EraseRecipientCommand command, CancellationToken ct)
    {
        var address = command.Address.Trim();
        var campaignRows = await _db.CampaignRecipients
            .Where(r => r.Address == address)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Address, RedactedMarker)
                .SetProperty(r => r.PersonalizationJson, (string?)null), ct);

        var seriesRows = await _db.CampaignSeriesRecipients
            .Where(r => r.Address == address)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Address, RedactedMarker)
                .SetProperty(r => r.PersonalizationJson, (string?)null), ct);

        return campaignRows + seriesRows;
    }
}
