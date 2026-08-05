using CampaignManager.Application.Abstractions;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Common;
using CampaignManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Campaigns.Queries.SearchCampaigns;

public sealed record SearchCampaignsQuery(
    string? Search,
    string? Status,
    string? Channel,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CampaignSummaryResponse>>;

public sealed class SearchCampaignsHandler
    : IRequestHandler<SearchCampaignsQuery, PagedResult<CampaignSummaryResponse>>
{
    private readonly IAppDbContext _db;

    public SearchCampaignsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<CampaignSummaryResponse>> Handle(
        SearchCampaignsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var campaigns = _db.Campaigns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            campaigns = campaigns.Where(c => c.Name.Contains(term) || c.TrackingId == term);
        }

        if (Enum.TryParse<CampaignStatus>(query.Status, ignoreCase: true, out var status))
        {
            campaigns = campaigns.Where(c => c.Status == status);
        }

        if (Enum.TryParse<Channel>(query.Channel, ignoreCase: true, out var channel))
        {
            campaigns = campaigns.Where(c => c.Channel == channel);
        }

        if (query.FromUtc is { } from) campaigns = campaigns.Where(c => c.CreatedAtUtc >= from);
        if (query.ToUtc is { } to) campaigns = campaigns.Where(c => c.CreatedAtUtc <= to);

        var total = await campaigns.CountAsync(ct);
        var items = await campaigns
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CampaignSummaryResponse(
                c.Id, c.TrackingId, c.Name, c.Channel.ToString(), c.Status.ToString(),
                c.TotalRecipients, c.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CampaignSummaryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
