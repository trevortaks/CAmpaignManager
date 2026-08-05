namespace CampaignManager.Contracts.Campaigns;

public sealed class CreateCampaignRequest
{
    public required string Name { get; init; }
    /// <summary>Sms, Email or WhatsApp.</summary>
    public required string Channel { get; init; }
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

public sealed class CampaignRecipientDto
{
    /// <summary>Phone number (SMS/WhatsApp) or email address.</summary>
    public required string Address { get; init; }
    /// <summary>Per-recipient placeholder values, e.g. {"FirstName": "Ada"}.</summary>
    public Dictionary<string, string>? Personalization { get; init; }
}
