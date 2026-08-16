using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Templates;

/// <summary>Mirrors CampaignManager.Application.Admin.Templates.TemplateSummary.</summary>
public sealed record TemplateSummary(
    Guid Id, string Name, CampaignChannel Channel, string? Subject, string Body, bool IsActive,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

/// <summary>Mirrors CampaignManager.Application.Admin.Templates.SaveTemplateInput.</summary>
public sealed class SaveTemplateInput
{
    public Guid? Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Sms, Email or WhatsApp.</summary>
    public required CampaignChannel Channel { get; init; }
    public string? Subject { get; init; }
    public required string Body { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>Body for <c>POST api/templates/preview</c>. Mirrors the nested record
/// TemplatesController.PreviewRequest. Supply either TemplateId (renders the stored template,
/// optionally overriding Body/Subject) or an ad-hoc Body/Subject with no TemplateId.</summary>
public sealed record PreviewTemplateRequest(
    Guid? TemplateId, string? Body, string? Subject, Dictionary<string, string>? SampleData);

/// <summary>Mirrors CampaignManager.Application.Admin.Templates.TemplatePreview.</summary>
public sealed record TemplatePreview(string? Subject, string Body);
