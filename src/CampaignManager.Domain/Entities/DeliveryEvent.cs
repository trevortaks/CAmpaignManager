using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.Entities;

public class DeliveryEvent
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public MessageStatus Status { get; set; }
    public string? Detail { get; set; }
    public string? ProviderRawPayload { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public Message? Message { get; set; }
}
