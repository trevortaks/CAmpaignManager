using CampaignManager.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Admin.Reports;

public sealed record ReportRow(
    DateOnly Date, string Channel, string Provider,
    int Sent, int Delivered, int Failed, int Rejected, int Expired, double? AvgDeliverySeconds);

public sealed record ReportSummary(
    int TotalSent, int TotalDelivered, int TotalFailed, double SuccessRate,
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<(string Channel, int Sent)> TopChannels,
    IReadOnlyList<(string Campaign, int Sent)> TopCampaigns);

public sealed record GetReportQuery(DateOnly FromDate, DateOnly ToDate) : IRequest<ReportSummary>;

public sealed class GetReportHandler : IRequestHandler<GetReportQuery, ReportSummary>
{
    private readonly IAppDbContext _db;

    public GetReportHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ReportSummary> Handle(GetReportQuery query, CancellationToken ct)
    {
        var providers = await _db.ProviderConfigurations.AsNoTracking().ToListAsync(ct);
        var stats = await _db.DailyStatistics.AsNoTracking()
            .Where(s => s.Date >= query.FromDate && s.Date <= query.ToDate)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

        // "Delivered" folds in Read (both represent a successfully received message for reporting).
        var rows = stats.Select(s => new ReportRow(
            s.Date, s.Channel.ToString(),
            providers.FirstOrDefault(p => p.Id == s.ProviderConfigurationId)?.Name ?? "(unassigned)",
            s.Sent, s.Delivered + s.Read, s.Failed, s.Rejected, s.Expired, s.AvgDeliverySeconds
        )).ToList();

        var totalSent = stats.Sum(s => s.Sent + s.Delivered + s.Read);
        var totalDelivered = stats.Sum(s => s.Delivered + s.Read);
        var totalFailed = stats.Sum(s => s.Failed + s.Rejected + s.Expired);
        var denominator = totalSent + totalFailed;

        var topChannels = stats
            .GroupBy(s => s.Channel.ToString())
            .Select(g => (Channel: g.Key, Sent: g.Sum(s => s.Sent + s.Delivered + s.Read)))
            .OrderByDescending(c => c.Sent)
            .ToList();

        var topCampaigns = await _db.Campaigns.AsNoTracking()
            .Where(c => c.CreatedAtUtc >= query.FromDate.ToDateTime(TimeOnly.MinValue)
                        && c.CreatedAtUtc < query.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue))
            .OrderByDescending(c => c.SentCount)
            .Take(10)
            .Select(c => new { c.Name, c.SentCount })
            .ToListAsync(ct);

        return new ReportSummary(
            totalSent, totalDelivered, totalFailed,
            denominator == 0 ? 0 : (double)totalSent / denominator,
            rows, topChannels,
            topCampaigns.Select(c => (c.Name, c.SentCount)).ToList());
    }
}
