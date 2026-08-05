using CampaignManager.Application.Jobs;
using Cronos;
using Hangfire;

namespace CampaignManager.Infrastructure.Jobs;

public sealed class HangfireSeriesScheduler : ISeriesScheduler
{
    private readonly IRecurringJobManager _recurringJobManager;

    public HangfireSeriesScheduler(IRecurringJobManager recurringJobManager)
    {
        _recurringJobManager = recurringJobManager;
    }

    public string Schedule(Guid organizationId, Guid seriesId, string cronExpression)
    {
        var jobId = $"series-{seriesId}";
        _recurringJobManager.AddOrUpdate<ICampaignSeriesJob>(
            jobId, j => j.RunAsync(organizationId, seriesId), cronExpression, new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
        return jobId;
    }

    public void Unschedule(string recurringJobId) => _recurringJobManager.RemoveIfExists(recurringJobId);

    public DateTime? GetNextOccurrence(string cronExpression, DateTime afterUtc)
    {
        try
        {
            var expression = CronExpression.Parse(cronExpression);
            return expression.GetNextOccurrence(afterUtc, TimeZoneInfo.Utc);
        }
        catch (CronFormatException)
        {
            return null;
        }
    }
}
