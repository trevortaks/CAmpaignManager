namespace CampaignManager.Application.Jobs;

/// <summary>Abstraction over the recurring-job system (Hangfire) so Application code can
/// schedule/unschedule a CampaignSeries occurrence without a Hangfire dependency.</summary>
public interface ISeriesScheduler
{
    /// <summary>Registers (or updates) the recurring job for a series. Returns the job id.</summary>
    string Schedule(Guid organizationId, Guid seriesId, string cronExpression);

    void Unschedule(string recurringJobId);

    /// <summary>Computes the next UTC occurrence after now for display purposes.</summary>
    DateTime? GetNextOccurrence(string cronExpression, DateTime afterUtc);
}
