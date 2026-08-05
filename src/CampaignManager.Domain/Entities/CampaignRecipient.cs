namespace CampaignManager.Domain.Entities;

public class CampaignRecipient
{
    public long Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Address { get; set; }
    public string? PersonalizationJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Campaign? Campaign { get; set; }
}
