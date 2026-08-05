namespace CampaignManager.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    /// <summary>SHA-256 hex hash of the full key; the plaintext is shown once at creation.</summary>
    public required string KeyHash { get; set; }
    /// <summary>First characters of the key (e.g. "cmk_a1b2c3"), indexed for lookup.</summary>
    public required string KeyPrefix { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Organization? Organization { get; set; }

    public bool IsActive(DateTime utcNow) =>
        RevokedAtUtc is null && (ExpiresAtUtc is null || ExpiresAtUtc > utcNow);
}
