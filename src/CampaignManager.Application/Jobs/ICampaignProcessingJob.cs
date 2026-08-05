namespace CampaignManager.Application.Jobs;

/// <summary>Campaign background jobs. Defined as an interface so API-side code can enqueue
/// Hangfire jobs by contract while the implementation lives in the Workers host.
/// All methods take organizationId explicitly — jobs have no ambient tenant.</summary>
public interface ICampaignProcessingJob
{
    /// <summary>Transitions the campaign to Processing and fans out send batches.</summary>
    Task DispatchAsync(Guid organizationId, Guid campaignId);

    /// <summary>Sends all still-Queued messages in the inclusive id range.</summary>
    Task SendBatchAsync(Guid organizationId, Guid campaignId, long firstMessageId, long lastMessageId);

    /// <summary>Computes final campaign status once no messages remain Queued/Processing;
    /// reschedules itself while work is outstanding.</summary>
    Task FinalizeAsync(Guid organizationId, Guid campaignId);

    /// <summary>POSTs the completion summary to the campaign's CallbackUrl (SSRF-guarded).</summary>
    Task NotifyCompletionAsync(Guid organizationId, Guid campaignId);
}
