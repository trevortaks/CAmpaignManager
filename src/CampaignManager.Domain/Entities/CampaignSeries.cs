using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.Entities;

/// <summary>A recurring campaign definition. On each cron occurrence, a new Campaign
/// instance is materialized from this template (same recipients, same content) and
/// dispatched through the normal campaign pipeline.</summary>
public class CampaignSeries
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public Channel Channel { get; set; }
    public required string Sender { get; set; }
    public string? Subject { get; set; }
    public string? MessageBody { get; set; }
    public Guid? TemplateId { get; set; }
    public string? CallbackUrl { get; set; }
    public int Priority { get; set; }

    /// <summary>Standard 5-field cron expression (UTC), e.g. "0 8 * * MON" for every Monday 08:00.</summary>
    public required string CronExpression { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Hangfire recurring job id while active; null while paused.</summary>
    public string? RecurringJobId { get; set; }

    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Organization? Organization { get; set; }
}
