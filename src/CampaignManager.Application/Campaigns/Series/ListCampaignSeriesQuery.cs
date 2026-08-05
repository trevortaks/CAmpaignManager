using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Campaigns.Series;

public sealed record ListCampaignSeriesQuery : IRequest<IReadOnlyList<CampaignSeriesSummary>>;

public sealed class ListCampaignSeriesHandler
    : IRequestHandler<ListCampaignSeriesQuery, IReadOnlyList<CampaignSeriesSummary>>
{
    private readonly IAppDbContext _db;

    public ListCampaignSeriesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CampaignSeriesSummary>> Handle(
        ListCampaignSeriesQuery query, CancellationToken ct) =>
        await _db.CampaignSeries.AsNoTracking()
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new CampaignSeriesSummary(
                s.Id, s.Name, s.Channel.ToString(), s.CronExpression, s.IsActive,
                s.LastRunAtUtc, s.NextRunAtUtc,
                _db.CampaignSeriesRecipients.Count(r => r.SeriesId == s.Id),
                s.CreatedAtUtc))
            .ToListAsync(ct);
}

public sealed record GetCampaignSeriesQuery(Guid SeriesId) : IRequest<SaveCampaignSeriesInput>;

public sealed class GetCampaignSeriesHandler : IRequestHandler<GetCampaignSeriesQuery, SaveCampaignSeriesInput>
{
    private readonly IAppDbContext _db;

    public GetCampaignSeriesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<SaveCampaignSeriesInput> Handle(GetCampaignSeriesQuery query, CancellationToken ct)
    {
        var series = await _db.CampaignSeries.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == query.SeriesId, ct)
            ?? throw new NotFoundException(nameof(CampaignSeries), query.SeriesId);
        var recipients = await _db.CampaignSeriesRecipients.AsNoTracking()
            .Where(r => r.SeriesId == series.Id)
            .Select(r => new SeriesRecipientInput(r.Address, null))
            .ToListAsync(ct);

        return new SaveCampaignSeriesInput
        {
            Id = series.Id,
            Name = series.Name,
            Channel = series.Channel.ToString(),
            Sender = series.Sender,
            Subject = series.Subject,
            MessageBody = series.MessageBody,
            TemplateId = series.TemplateId,
            CallbackUrl = series.CallbackUrl,
            Priority = series.Priority,
            CronExpression = series.CronExpression,
            IsActive = series.IsActive,
            Recipients = recipients
        };
    }
}
