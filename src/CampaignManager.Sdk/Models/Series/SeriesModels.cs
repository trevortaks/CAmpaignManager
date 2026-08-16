using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Series;

/// <summary>Mirrors CampaignManager.Application.Campaigns.Series.CampaignSeriesSummary.</summary>
public sealed record CampaignSeriesSummary(
    Guid Id, string Name, CampaignChannel Channel, string CronExpression, bool IsActive,
    DateTime? LastRunAtUtc, DateTime? NextRunAtUtc, int RecipientCount, DateTime CreatedAtUtc);

/// <summary>Mirrors CampaignManager.Application.Campaigns.Series.SaveCampaignSeriesInput. Used as
/// both the create/update request body and the GET-by-id response shape (the API is asymmetric
/// here: reading a series returns the same shape used to write one).</summary>
public sealed class SaveCampaignSeriesInput
{
    public Guid? Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Sms, Email or WhatsApp.</summary>
    public required CampaignChannel Channel { get; init; }
    public required string Sender { get; init; }
    public string? Subject { get; init; }
    public string? MessageBody { get; init; }
    public Guid? TemplateId { get; init; }
    public string? CallbackUrl { get; init; }
    public int Priority { get; init; }
    /// <summary>Standard 5-field cron expression (see Cronos), e.g. "0 9 * * MON" for every Monday at 09:00 UTC.</summary>
    public required string CronExpression { get; init; }
    public bool IsActive { get; init; } = true;
    public required List<SeriesRecipientInput> Recipients { get; init; }
}

/// <summary>Mirrors CampaignManager.Application.Campaigns.Series.SeriesRecipientInput.</summary>
public sealed record SeriesRecipientInput(string Address, Dictionary<string, string>? Personalization);
