using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Campaigns;

/// <summary>SDK-only convenience type bundling the query parameters for <c>GET api/campaigns</c>.
/// Not a wire type. A record so <see cref="Clients.CampaignsClient.SearchAllAsync"/> can advance
/// the page via a <c>with</c>-expression.</summary>
public sealed record CampaignSearchOptions
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public CampaignChannel? Channel { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
