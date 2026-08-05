using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Jobs;
using Hangfire;

namespace CampaignManager.Infrastructure.Jobs;

public sealed class HangfireCampaignDispatcher : ICampaignDispatcher
{
    private readonly IBackgroundJobClient _client;

    public HangfireCampaignDispatcher(IBackgroundJobClient client)
    {
        _client = client;
    }

    public string EnqueueDispatch(Guid organizationId, Guid campaignId) =>
        _client.Enqueue<ICampaignProcessingJob>(j => j.DispatchAsync(organizationId, campaignId));

    public string ScheduleDispatch(Guid organizationId, Guid campaignId, DateTime scheduledAtUtc) =>
        _client.Schedule<ICampaignProcessingJob>(
            j => j.DispatchAsync(organizationId, campaignId),
            new DateTimeOffset(DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc)));

    public bool DeleteScheduledJob(string jobId) => _client.Delete(jobId);
}
