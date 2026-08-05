using CampaignManager.Application.Abstractions;
using CampaignManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;
    private readonly INotificationSink _sink;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IAppDbContext db, INotificationSink sink, ILogger<NotificationService> logger)
    {
        _db = db;
        _sink = sink;
        _logger = logger;
    }

    public async Task NotifyAsync(
        Guid organizationId, string eventType, string subject, string body, CancellationToken ct)
    {
        var settings = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

        if (!IsEnabled(settings, eventType))
        {
            return;
        }

        var log = new NotificationLog
        {
            OrganizationId = organizationId,
            EventType = eventType,
            Subject = subject,
            Body = body,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(settings?.RecipientEmail))
        {
            log.Sent = false;
            log.Error = "No notification recipient email configured for this organization.";
        }
        else
        {
            try
            {
                await _sink.SendAsync(settings.RecipientEmail, subject, body, ct);
                log.Sent = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Sent = false;
                log.Error = ex.Message;
                _logger.LogWarning(ex, "Failed to send {EventType} notification for org {OrganizationId}",
                    eventType, organizationId);
            }
        }

        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Public for direct unit testing; the gating rules are pure and worth
    /// verifying in isolation from the database/mail plumbing.</summary>
    public static bool IsEnabled(NotificationSettings? settings, string eventType)
    {
        // Sensible defaults when an organization has never configured notifications.
        if (settings is null)
        {
            return eventType is NotificationEventTypes.ProviderOffline
                or NotificationEventTypes.ProviderAuthFailed
                or NotificationEventTypes.CampaignFailed
                or NotificationEventTypes.HighFailureRate;
        }

        return eventType switch
        {
            NotificationEventTypes.ProviderOffline or NotificationEventTypes.ProviderAuthFailed
                => settings.NotifyProviderOffline,
            NotificationEventTypes.CampaignCompleted => settings.NotifyCampaignCompleted,
            NotificationEventTypes.CampaignFailed or NotificationEventTypes.HighFailureRate
                => settings.NotifyCampaignFailed,
            _ => false
        };
    }
}
