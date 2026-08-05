using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Campaigns.Queries.GetCampaignStatus;

/// <summary>Fetches campaign status by CampaignId or TrackingId (exactly one must be set).</summary>
public sealed record GetCampaignStatusQuery(Guid? CampaignId, string? TrackingId)
    : IRequest<CampaignStatusResponse>;

public sealed class GetCampaignStatusHandler
    : IRequestHandler<GetCampaignStatusQuery, CampaignStatusResponse>
{
    private readonly IAppDbContext _db;

    public GetCampaignStatusHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<CampaignStatusResponse> Handle(GetCampaignStatusQuery query, CancellationToken ct)
    {
        var campaign = query.CampaignId is { } id
            ? await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            : await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.TrackingId == query.TrackingId, ct);

        if (campaign is null)
        {
            throw new NotFoundException(nameof(Campaign), (object?)query.CampaignId ?? query.TrackingId ?? "?");
        }

        var statusCounts = await _db.Messages.AsNoTracking()
            .Where(m => m.CampaignId == campaign.Id)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(params MessageStatus[] statuses) =>
            statusCounts.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

        var failureReasons = await _db.Messages.AsNoTracking()
            .Where(m => m.CampaignId == campaign.Id && m.LastError != null &&
                        (m.Status == MessageStatus.Failed || m.Status == MessageStatus.Rejected))
            .GroupBy(m => m.LastError!)
            .Select(g => new { Error = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        return new CampaignStatusResponse
        {
            CampaignId = campaign.Id,
            TrackingId = campaign.TrackingId,
            Name = campaign.Name,
            Channel = campaign.Channel.ToString(),
            Status = campaign.Status.ToString(),
            ScheduledAtUtc = campaign.ScheduledAtUtc,
            StartedAtUtc = campaign.StartedAtUtc,
            CompletedAtUtc = campaign.CompletedAtUtc,
            Statistics = new CampaignStatistics
            {
                Total = campaign.TotalRecipients,
                Queued = Count(MessageStatus.Queued),
                Processing = Count(MessageStatus.Processing),
                Sent = Count(MessageStatus.Sent),
                Delivered = Count(MessageStatus.Delivered),
                Read = Count(MessageStatus.Read),
                Failed = Count(MessageStatus.Failed),
                Rejected = Count(MessageStatus.Rejected),
                Expired = Count(MessageStatus.Expired)
            },
            FailureReasons = failureReasons.Select(f => new FailureReason(f.Error, f.Count)).ToList()
        };
    }
}
