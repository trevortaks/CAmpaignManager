namespace CampaignManager.Application.Notifications;

public static class NotificationEventTypes
{
    public const string ProviderOffline = "ProviderOffline";
    public const string ProviderAuthFailed = "ProviderAuthFailed";
    public const string CampaignCompleted = "CampaignCompleted";
    public const string CampaignFailed = "CampaignFailed";
    public const string HighFailureRate = "HighFailureRate";
}

/// <summary>Raises an admin notification for a known event type, subject to the
/// organization's NotificationSettings gates. Always records a NotificationLog row (sent or
/// not) so admins can audit what fired even without email configured.</summary>
public interface INotificationService
{
    Task NotifyAsync(Guid organizationId, string eventType, string subject, string body, CancellationToken ct);
}

/// <summary>The actual outbound channel (SMTP, webhook, ...). Kept separate from
/// INotificationService so the gating/logging logic is testable without a mail server.</summary>
public interface INotificationSink
{
    Task SendAsync(string recipientEmail, string subject, string body, CancellationToken ct);
}
