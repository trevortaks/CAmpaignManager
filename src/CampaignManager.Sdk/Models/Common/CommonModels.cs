namespace CampaignManager.Sdk.Models.Common;

/// <summary>Mirrors CampaignManager.Contracts.Common.PagedResult&lt;T&gt;.</summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
