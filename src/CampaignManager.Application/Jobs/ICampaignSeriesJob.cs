namespace CampaignManager.Application.Jobs;

/// <summary>Hangfire recurring job entry point: materializes and dispatches one occurrence
/// of a CampaignSeries.</summary>
public interface ICampaignSeriesJob
{
    Task RunAsync(Guid organizationId, Guid seriesId);
}
