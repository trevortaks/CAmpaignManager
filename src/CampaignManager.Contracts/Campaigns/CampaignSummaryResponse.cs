namespace CampaignManager.Contracts.Campaigns;

public sealed record CampaignSummaryResponse(
    Guid CampaignId,
    string TrackingId,
    string Name,
    string Channel,
    string Status,
    int TotalRecipients,
    DateTime CreatedAtUtc);
