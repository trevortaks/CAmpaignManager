using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Campaigns;

/// <summary>Mirrors CampaignManager.Contracts.Campaigns.CampaignSummaryResponse.</summary>
public sealed record CampaignSummaryResponse(
    Guid CampaignId,
    string TrackingId,
    string Name,
    CampaignChannel Channel,
    string Status,
    int TotalRecipients,
    DateTime CreatedAtUtc);
