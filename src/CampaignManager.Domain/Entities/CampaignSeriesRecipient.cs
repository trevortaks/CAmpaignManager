namespace CampaignManager.Domain.Entities;

public class CampaignSeriesRecipient
{
    public long Id { get; set; }
    public Guid SeriesId { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Address { get; set; }
    public string? PersonalizationJson { get; set; }

    public CampaignSeries? Series { get; set; }
}
