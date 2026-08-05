namespace CampaignManager.Domain.Entities;

/// <summary>A provider callback that could not be matched to a message (e.g. it arrived
/// before the ProviderMessageId was persisted). A recurring job replays unresolved rows.</summary>
public class WebhookDeadLetter
{
    public long Id { get; set; }
    public required string ProviderKey { get; set; }
    public required string ProviderMessageId { get; set; }
    public required string ReportedStatus { get; set; }
    public string? Detail { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public int ReplayCount { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? AbandonedAtUtc { get; set; }
}
