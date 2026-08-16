using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Compliance;

/// <summary>Mirrors CampaignManager.Application.Compliance.SuppressionSummary.</summary>
public sealed record SuppressionSummary(Guid Id, string Address, CampaignChannel? Channel, string Reason, DateTime CreatedAtUtc);
