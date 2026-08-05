using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Jobs;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.StateMachine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Jobs;

public sealed class MaintenanceJobs : IMaintenanceJobs
{
    private const int MaxReplayAttempts = 10;

    private readonly IAppDbContext _db;
    private readonly ILogger<MaintenanceJobs> _logger;

    public MaintenanceJobs(IAppDbContext db, ILogger<MaintenanceJobs> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ReplayWebhookDeadLettersAsync()
    {
        var pending = await _db.WebhookDeadLetters
            .Where(d => d.ResolvedAtUtc == null && d.AbandonedAtUtc == null)
            .OrderBy(d => d.Id)
            .Take(500)
            .ToListAsync();
        if (pending.Count == 0) return;

        var resolved = 0;
        foreach (var deadLetter in pending)
        {
            var message = await _db.Messages.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.ProviderMessageId == deadLetter.ProviderMessageId);
            deadLetter.ReplayCount++;

            if (message is null)
            {
                if (deadLetter.ReplayCount >= MaxReplayAttempts)
                {
                    deadLetter.AbandonedAtUtc = DateTime.UtcNow;
                }

                continue;
            }

            if (Enum.TryParse<MessageStatus>(deadLetter.ReportedStatus, ignoreCase: true, out var status))
            {
                var occurred = deadLetter.OccurredAtUtc ?? deadLetter.ReceivedAtUtc;
                if (message.TryApplyWebhookStatus(status, occurred))
                {
                    _db.DeliveryEvents.Add(new DeliveryEvent
                    {
                        MessageId = message.Id,
                        Status = status,
                        Detail = deadLetter.Detail,
                        OccurredAtUtc = occurred
                    });
                }
            }

            deadLetter.ResolvedAtUtc = DateTime.UtcNow;
            resolved++;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Webhook dead-letter replay: {Resolved} resolved, {Pending} still pending",
            resolved, pending.Count - resolved);
    }

    public async Task SweepStuckCampaignsAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-10);
        var stuck = await _db.Campaigns.IgnoreQueryFilters()
            .Where(c => c.Status == CampaignStatus.Processing && c.StartedAtUtc < threshold)
            .Take(100)
            .ToListAsync();

        foreach (var campaign in stuck)
        {
            var counts = await _db.Messages.IgnoreQueryFilters()
                .Where(m => m.CampaignId == campaign.Id)
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int Count(params MessageStatus[] statuses) =>
                counts.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

            if (Count(MessageStatus.Queued, MessageStatus.Processing) > 0) continue; // still working

            var total = counts.Sum(c => c.Count);
            var failed = Count(MessageStatus.Failed, MessageStatus.Rejected, MessageStatus.Expired);
            campaign.SentCount = Count(MessageStatus.Sent, MessageStatus.Delivered, MessageStatus.Read);
            campaign.DeliveredCount = Count(MessageStatus.Delivered, MessageStatus.Read);
            campaign.FailedCount = failed;
            campaign.TransitionTo(CampaignStateMachine.Finalize(total, failed));
            campaign.CompletedAtUtc = DateTime.UtcNow;
            _logger.LogWarning("Stuck-campaign sweep finalized campaign {CampaignId} as {Status}",
                campaign.Id, campaign.Status);
        }

        await _db.SaveChangesAsync();
    }

    public async Task RollupDailyStatisticsAsync()
    {
        // Re-aggregate the last two UTC days so late webhooks are folded in.
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue);

        var aggregates = await _db.Messages.IgnoreQueryFilters()
            .Where(m => m.QueuedAtUtc >= fromUtc)
            .GroupBy(m => new
            {
                m.OrganizationId,
                Date = DateOnly.FromDateTime(m.QueuedAtUtc),
                m.Channel,
                m.ProviderConfigurationId
            })
            .Select(g => new
            {
                g.Key,
                Queued = g.Count(m => m.Status == MessageStatus.Queued || m.Status == MessageStatus.Processing),
                Sent = g.Count(m => m.Status == MessageStatus.Sent),
                Delivered = g.Count(m => m.Status == MessageStatus.Delivered),
                Read = g.Count(m => m.Status == MessageStatus.Read),
                Failed = g.Count(m => m.Status == MessageStatus.Failed),
                Rejected = g.Count(m => m.Status == MessageStatus.Rejected),
                Expired = g.Count(m => m.Status == MessageStatus.Expired),
                AvgDeliverySeconds = g
                    .Where(m => m.DeliveredAtUtc != null && m.SentAtUtc != null)
                    .Average(m => (double?)EF.Functions.DateDiffSecond(m.SentAtUtc!.Value, m.DeliveredAtUtc!.Value))
            })
            .ToListAsync();

        var existing = await _db.DailyStatistics
            .Where(s => s.Date >= fromDate)
            .ToListAsync();

        foreach (var aggregate in aggregates)
        {
            var row = existing.FirstOrDefault(s =>
                s.OrganizationId == aggregate.Key.OrganizationId &&
                s.Date == aggregate.Key.Date &&
                s.Channel == aggregate.Key.Channel &&
                s.ProviderConfigurationId == aggregate.Key.ProviderConfigurationId);
            if (row is null)
            {
                row = new DailyStatistic
                {
                    OrganizationId = aggregate.Key.OrganizationId,
                    Date = aggregate.Key.Date,
                    Channel = aggregate.Key.Channel,
                    ProviderConfigurationId = aggregate.Key.ProviderConfigurationId
                };
                _db.DailyStatistics.Add(row);
            }

            row.Queued = aggregate.Queued;
            row.Sent = aggregate.Sent;
            row.Delivered = aggregate.Delivered;
            row.Read = aggregate.Read;
            row.Failed = aggregate.Failed;
            row.Rejected = aggregate.Rejected;
            row.Expired = aggregate.Expired;
            row.AvgDeliverySeconds = aggregate.AvgDeliverySeconds;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Daily statistics rollup updated {Count} rows", aggregates.Count);
    }
}
