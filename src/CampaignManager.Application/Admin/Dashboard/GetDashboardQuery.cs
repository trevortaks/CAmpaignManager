using CampaignManager.Application.Abstractions;
using CampaignManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Admin.Dashboard;

public sealed record DashboardData(
    int CampaignsToday,
    int SentToday,
    int FailedToday,
    int PendingMessages,
    int ActiveProviders,
    IReadOnlyList<DailyPoint> SentTrend,          // last 14 days
    IReadOnlyList<DailyPoint> FailedTrend,        // last 14 days
    IReadOnlyList<ChannelCount> ByChannel,        // last 30 days
    IReadOnlyList<ProviderCount> ByProvider,      // last 30 days
    IReadOnlyList<ProviderHealth> ProviderHealthList,
    IReadOnlyList<CampaignRow> RecentCampaigns,
    IReadOnlyList<CampaignRow> ScheduledCampaigns);

public sealed record DailyPoint(DateOnly Date, int Count);
public sealed record ChannelCount(string Channel, int Sent, int Failed);
public sealed record ProviderCount(string Provider, int Sent);
public sealed record ProviderHealth(
    string Name, string Channel, bool IsEnabled, DateTime? LastTestedAtUtc, bool? LastTestSucceeded);
public sealed record CampaignRow(
    Guid Id, string Name, string Channel, string Status, int Total, DateTime? WhenUtc);

public sealed record GetDashboardQuery : IRequest<DashboardData>;

public sealed class GetDashboardHandler : IRequestHandler<GetDashboardQuery, DashboardData>
{
    private readonly IAppDbContext _db;

    public GetDashboardHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardData> Handle(GetDashboardQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var trendStart = today.AddDays(-13);
        var monthStart = today.AddDays(-29);

        var campaignsToday = await _db.Campaigns.CountAsync(c => c.CreatedAtUtc >= todayStart, ct);

        // Today's outcomes come live from Messages (the rollup runs hourly); history from the rollup.
        var todaysCounts = await _db.Messages
            .Where(m => m.QueuedAtUtc >= todayStart)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int TodayCount(params MessageStatus[] statuses) =>
            todaysCounts.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

        var stats = await _db.DailyStatistics
            .Where(s => s.Date >= monthStart)
            .ToListAsync(ct);

        var sentTrend = new List<DailyPoint>();
        var failedTrend = new List<DailyPoint>();
        for (var date = trendStart; date <= today; date = date.AddDays(1))
        {
            var rows = stats.Where(s => s.Date == date).ToList();
            var sent = rows.Sum(s => s.Sent + s.Delivered + s.Read);
            var failed = rows.Sum(s => s.Failed + s.Rejected + s.Expired);
            if (date == today)
            {
                sent = Math.Max(sent, TodayCount(MessageStatus.Sent, MessageStatus.Delivered, MessageStatus.Read));
                failed = Math.Max(failed, TodayCount(MessageStatus.Failed, MessageStatus.Rejected, MessageStatus.Expired));
            }

            sentTrend.Add(new DailyPoint(date, sent));
            failedTrend.Add(new DailyPoint(date, failed));
        }

        var byChannel = stats
            .GroupBy(s => s.Channel)
            .Select(g => new ChannelCount(
                g.Key.ToString(),
                g.Sum(s => s.Sent + s.Delivered + s.Read),
                g.Sum(s => s.Failed + s.Rejected + s.Expired)))
            .OrderBy(c => c.Channel)
            .ToList();

        var providers = await _db.ProviderConfigurations.AsNoTracking().ToListAsync(ct);
        var byProvider = stats
            .Where(s => s.ProviderConfigurationId != null)
            .GroupBy(s => s.ProviderConfigurationId)
            .Select(g => new ProviderCount(
                providers.FirstOrDefault(p => p.Id == g.Key)?.Name ?? "(deleted)",
                g.Sum(s => s.Sent + s.Delivered + s.Read)))
            .OrderByDescending(p => p.Sent)
            .Take(8)
            .ToList();

        var pending = await _db.Messages.CountAsync(
            m => m.Status == MessageStatus.Queued || m.Status == MessageStatus.Processing, ct);

        var recent = await _db.Campaigns.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(10)
            .Select(c => new CampaignRow(
                c.Id, c.Name, c.Channel.ToString(), c.Status.ToString(), c.TotalRecipients, c.CreatedAtUtc))
            .ToListAsync(ct);

        var scheduled = await _db.Campaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Scheduled)
            .OrderBy(c => c.ScheduledAtUtc)
            .Take(10)
            .Select(c => new CampaignRow(
                c.Id, c.Name, c.Channel.ToString(), c.Status.ToString(), c.TotalRecipients, c.ScheduledAtUtc))
            .ToListAsync(ct);

        return new DashboardData(
            campaignsToday,
            TodayCount(MessageStatus.Sent, MessageStatus.Delivered, MessageStatus.Read),
            TodayCount(MessageStatus.Failed, MessageStatus.Rejected, MessageStatus.Expired),
            pending,
            providers.Count(p => p.IsEnabled),
            sentTrend,
            failedTrend,
            byChannel,
            byProvider,
            providers.Select(p => new ProviderHealth(
                p.Name, p.Channel.ToString(), p.IsEnabled, p.LastTestedAtUtc, p.LastTestSucceeded)).ToList(),
            recent,
            scheduled);
    }
}
