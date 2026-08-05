namespace CampaignManager.Domain.Enums;

public enum MessageStatus : byte
{
    Queued = 0,
    Processing = 1,
    Sent = 2,
    Delivered = 3,
    Read = 4,
    Failed = 5,
    Expired = 6,
    Rejected = 7,
    Unknown = 8
}
