using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Campaigns;

/// <summary>Body for <c>POST api/campaigns</c>. Mirrors CampaignManager.Contracts.Campaigns.CreateCampaignRequest.
/// <para>The SDK does not retry this POST by default: the server has no persisted idempotency-key
/// mechanism, so retrying after an ambiguous failure can create two campaigns.</para></summary>
public sealed class CreateCampaignRequest
{
    public required string Name { get; init; }
    /// <summary>Sms, Email or WhatsApp.</summary>
    public required CampaignChannel Channel { get; init; }
    public DateTime? ScheduledAtUtc { get; init; }
    public required string Sender { get; init; }
    /// <summary>Email only.</summary>
    public string? Subject { get; init; }
    /// <summary>Inline body; required when TemplateId is not supplied. Supports {{Placeholder}} tokens.</summary>
    public string? MessageBody { get; init; }
    public Guid? TemplateId { get; init; }
    public required List<CampaignRecipientDto> Recipients { get; init; }
    public List<string>? Tags { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public int Priority { get; init; }
    public string? CallbackUrl { get; init; }
}

/// <summary>Mirrors CampaignManager.Contracts.Campaigns.CampaignRecipientDto.</summary>
public sealed class CampaignRecipientDto
{
    /// <summary>Phone number (SMS/WhatsApp) or email address.</summary>
    public required string Address { get; init; }
    /// <summary>Per-recipient placeholder values, e.g. {"FirstName": "Ada"}.</summary>
    public Dictionary<string, string>? Personalization { get; init; }
}
