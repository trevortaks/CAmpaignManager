using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Campaigns;

/// <summary>Mirrors CampaignManager.Contracts.Campaigns.CampaignStatusResponse.</summary>
public sealed class CampaignStatusResponse
{
    public Guid CampaignId { get; init; }
    public required string TrackingId { get; init; }
    public required string Name { get; init; }
    public required CampaignChannel Channel { get; init; }
    public required string Status { get; init; }
    public DateTime? ScheduledAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public required CampaignStatistics Statistics { get; init; }
    public List<FailureReason> FailureReasons { get; init; } = [];
}

/// <summary>Mirrors CampaignManager.Contracts.Campaigns.CampaignStatistics.</summary>
public sealed class CampaignStatistics
{
    public int Total { get; init; }
    public int Queued { get; init; }
    public int Processing { get; init; }
    public int Sent { get; init; }
    public int Delivered { get; init; }
    public int Read { get; init; }
    public int Failed { get; init; }
    public int Rejected { get; init; }
    public int Expired { get; init; }
}

/// <summary>Mirrors CampaignManager.Contracts.Campaigns.FailureReason.</summary>
public sealed record FailureReason(string Error, int Count);
