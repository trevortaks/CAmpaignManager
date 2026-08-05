using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.Entities;

/// <summary>An address a tenant must never message again (opt-out/STOP/complaint). Checked
/// once per dispatch batch, not per-message, to avoid an extra query per send.</summary>
public class Suppression
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Address { get; set; }
    /// <summary>Null = suppressed on every channel; set = only that channel.</summary>
    public Channel? Channel { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
