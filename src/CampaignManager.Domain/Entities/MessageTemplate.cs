using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.Entities;

public class MessageTemplate
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public Channel Channel { get; set; }
    public string? Subject { get; set; }
    public required string Body { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Organization? Organization { get; set; }
}
