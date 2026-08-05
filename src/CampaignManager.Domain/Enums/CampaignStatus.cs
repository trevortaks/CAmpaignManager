namespace CampaignManager.Domain.Enums;

public enum CampaignStatus : byte
{
    Draft = 0,
    Scheduled = 1,
    Queued = 2,
    Processing = 3,
    Completed = 4,
    CompletedWithErrors = 5,
    Failed = 6,
    Cancelled = 7
}
