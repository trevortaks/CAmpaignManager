namespace CampaignManager.Application.Campaigns.Series;

public sealed record CampaignSeriesSummary(
    Guid Id, string Name, string Channel, string CronExpression, bool IsActive,
    DateTime? LastRunAtUtc, DateTime? NextRunAtUtc, int RecipientCount, DateTime CreatedAtUtc);

public sealed class SaveCampaignSeriesInput
{
    public Guid? Id { get; init; }
    public required string Name { get; init; }
    public required string Channel { get; init; }
    public required string Sender { get; init; }
    public string? Subject { get; init; }
    public string? MessageBody { get; init; }
    public Guid? TemplateId { get; init; }
    public string? CallbackUrl { get; init; }
    public int Priority { get; init; }
    public required string CronExpression { get; init; }
    public bool IsActive { get; init; } = true;
    public required List<SeriesRecipientInput> Recipients { get; init; }
}

public sealed record SeriesRecipientInput(string Address, Dictionary<string, string>? Personalization);
