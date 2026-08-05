namespace CampaignManager.Contracts.Campaigns;

public sealed record CreateCampaignResponse(Guid CampaignId, string TrackingId, string Status);
