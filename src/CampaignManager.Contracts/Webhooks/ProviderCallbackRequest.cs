namespace CampaignManager.Contracts.Webhooks;

public sealed class ProviderCallbackRequest
{
    /// <summary>The provider's message id returned at send time (e.g. Twilio SID).</summary>
    public required string ProviderMessageId { get; init; }
    /// <summary>Sent, Delivered, Read, Failed, Expired, Unknown.</summary>
    public required string Status { get; init; }
    public string? Detail { get; init; }
    public DateTime? OccurredAtUtc { get; init; }
}
