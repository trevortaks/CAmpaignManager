using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Models.Campaigns.Import;

/// <summary>Body for <c>POST api/campaigns/import</c> (multipart CSV upload). SDK-only request
/// type — the server endpoint takes multipart form fields, not JSON. The server enforces a
/// 100MB request size limit; oversized files fail with a 413 before validation runs.</summary>
public sealed class ImportCampaignRequest
{
    public required string Name { get; init; }
    /// <summary>Sms, Email or WhatsApp.</summary>
    public required CampaignChannel Channel { get; init; }
    public required string Sender { get; init; }
    /// <summary>CSV content. Caller-provided streams remain open after import. Streams created by
    /// <see cref="FromFile"/> and <see cref="FromBytes"/> are owned and closed by the import call.</summary>
    public required Stream FileContent { get; init; }
    internal bool OwnsFileContent { get; init; }
    public required string FileName { get; init; }
    /// <summary>Content-Type header for the uploaded file part. The server only reads bytes, so
    /// this is cosmetic; defaults to "text/csv".</summary>
    public string ContentType { get; init; } = "text/csv";
    public string? MessageBody { get; init; }
    public Guid? TemplateId { get; init; }
    public string? Subject { get; init; }
    public DateTime? ScheduledAtUtc { get; init; }
    public string? CallbackUrl { get; init; }

    /// <summary>Builds a request that streams the CSV directly from disk. The SDK closes the file
    /// after <c>ImportAsync</c> completes.</summary>
    public static ImportCampaignRequest FromFile(
        string filePath, string name, CampaignChannel channel, string sender,
        string? messageBody = null, Guid? templateId = null, string? subject = null,
        DateTime? scheduledAtUtc = null, string? callbackUrl = null) =>
        new()
        {
            Name = name,
            Channel = channel,
            Sender = sender,
            FileContent = File.OpenRead(filePath),
            OwnsFileContent = true,
            FileName = Path.GetFileName(filePath),
            MessageBody = messageBody,
            TemplateId = templateId,
            Subject = subject,
            ScheduledAtUtc = scheduledAtUtc,
            CallbackUrl = callbackUrl
        };

    /// <summary>Builds a request from an in-memory CSV payload.</summary>
    public static ImportCampaignRequest FromBytes(
        byte[] csvBytes, string fileName, string name, CampaignChannel channel, string sender,
        string? messageBody = null, Guid? templateId = null, string? subject = null,
        DateTime? scheduledAtUtc = null, string? callbackUrl = null) =>
        new()
        {
            Name = name,
            Channel = channel,
            Sender = sender,
            FileContent = new MemoryStream(csvBytes),
            OwnsFileContent = true,
            FileName = fileName,
            MessageBody = messageBody,
            TemplateId = templateId,
            Subject = subject,
            ScheduledAtUtc = scheduledAtUtc,
            CallbackUrl = callbackUrl
        };
}
