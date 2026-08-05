using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.Entities;

/// <summary>Pre-aggregated per-day message statistics written by the rollup job.
/// Dashboards and reports read this table, never the Messages table.</summary>
public class DailyStatistic
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateOnly Date { get; set; }
    public Channel Channel { get; set; }
    public Guid? ProviderConfigurationId { get; set; }
    public int Queued { get; set; }
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Read { get; set; }
    public int Failed { get; set; }
    public int Rejected { get; set; }
    public int Expired { get; set; }
    public double? AvgDeliverySeconds { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
