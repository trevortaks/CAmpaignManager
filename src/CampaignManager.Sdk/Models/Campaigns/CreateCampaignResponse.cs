namespace CampaignManager.Sdk.Models.Campaigns;

/// <summary>Mirrors CampaignManager.Contracts.Campaigns.CreateCampaignResponse.</summary>
public sealed record CreateCampaignResponse(Guid CampaignId, string TrackingId, string Status);
