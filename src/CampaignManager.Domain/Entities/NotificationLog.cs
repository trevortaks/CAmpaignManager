namespace CampaignManager.Domain.Entities;

/// <summary>Record of an admin-facing notification (provider offline, campaign failed, etc.)
/// and whether delivery of the notification itself succeeded.</summary>
public class NotificationLog
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string EventType { get; set; }   // ProviderOffline, ProviderAuthFailed, CampaignCompleted, CampaignFailed, HighFailureRate
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public bool Sent { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Per-organization notification routing configuration.</summary>
public class NotificationSettings
{
    public Guid OrganizationId { get; set; }
    public string? RecipientEmail { get; set; }
    public bool NotifyProviderOffline { get; set; } = true;
    public bool NotifyCampaignCompleted { get; set; }
    public bool NotifyCampaignFailed { get; set; } = true;
    /// <summary>Fraction (0–1) of failed messages in a completed campaign that triggers an alert.</summary>
    public double HighFailureRateThreshold { get; set; } = 0.25;

    public Organization? Organization { get; set; }
}
