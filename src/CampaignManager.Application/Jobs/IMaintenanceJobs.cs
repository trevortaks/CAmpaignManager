namespace CampaignManager.Application.Jobs;

/// <summary>Recurring maintenance jobs hosted by the Workers Hangfire server.</summary>
public interface IMaintenanceJobs
{
    /// <summary>Re-attempts unmatched provider callbacks (dead letters); abandons after
    /// repeated failures.</summary>
    Task ReplayWebhookDeadLettersAsync();

    /// <summary>Safety net: finalizes campaigns stuck in Processing whose messages are all
    /// terminal (e.g. a lost finalizer job).</summary>
    Task SweepStuckCampaignsAsync();

    /// <summary>Aggregates yesterday's + today's message outcomes into DailyStatistics.</summary>
    Task RollupDailyStatisticsAsync();
}
