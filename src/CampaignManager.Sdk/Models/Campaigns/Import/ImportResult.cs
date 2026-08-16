namespace CampaignManager.Sdk.Models.Campaigns.Import;

/// <summary>Mirrors the nested record CampaignImportController.ImportResult.</summary>
public sealed record ImportResult(
    Guid CampaignId,
    string TrackingId,
    string Status,
    int AcceptedRecipients,
    int InvalidRows,
    int DuplicateRows,
    IReadOnlyList<ImportRowError> Errors);

/// <summary>Mirrors the nested record CampaignImportController.ImportRowError.</summary>
public sealed record ImportRowError(int Line, string Reason);
