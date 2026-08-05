namespace CampaignManager.Application.Abstractions;

/// <summary>Abstraction over the background job system (Hangfire) so Application code
/// can enqueue/schedule campaign processing without a Hangfire dependency.</summary>
public interface ICampaignDispatcher
{
    /// <summary>Enqueues immediate dispatch. Returns the job id.</summary>
    string EnqueueDispatch(Guid organizationId, Guid campaignId);

    /// <summary>Schedules dispatch for a future time. Returns the job id (store it for cancellation).</summary>
    string ScheduleDispatch(Guid organizationId, Guid campaignId, DateTime scheduledAtUtc);

    /// <summary>Deletes a scheduled job; returns false if it no longer exists.</summary>
    bool DeleteScheduledJob(string jobId);
}
