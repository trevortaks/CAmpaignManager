using System.Globalization;
using CampaignManager.Application.Campaigns.Commands.CreateCampaign;
using CampaignManager.Contracts.Campaigns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampaignManager.Api.Controllers;

/// <summary>CSV recipient import. The file is parsed as a stream (never fully buffered);
/// the first column (or a column named "address") is the recipient address and every other
/// header becomes a personalization key. Returns a validation report alongside the created
/// campaign.</summary>
[ApiController]
[Route("api/campaigns")]
[Authorize(Policy = "ApiAccess")]
[EnableRateLimiting("campaigns")]
public sealed class CampaignImportController : ControllerBase
{
    private const int MaxReportedErrors = 100;

    private readonly ISender _sender;

    public CampaignImportController(ISender sender)
    {
        _sender = sender;
    }

    public sealed record ImportResult(
        Guid CampaignId, string TrackingId, string Status,
        int AcceptedRecipients, int InvalidRows, int DuplicateRows,
        IReadOnlyList<ImportRowError> Errors);

    public sealed record ImportRowError(int Line, string Reason);

    [HttpPost("import")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType<ImportResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(
        [FromForm] string name,
        [FromForm] string channel,
        [FromForm] string sender,
        IFormFile file,
        [FromForm] string? messageBody = null,
        [FromForm] Guid? templateId = null,
        [FromForm] string? subject = null,
        [FromForm] DateTime? scheduledAtUtc = null,
        [FromForm] string? callbackUrl = null,
        CancellationToken ct = default)
    {
        if (file.Length == 0)
        {
            return Problem(statusCode: 400, title: "Empty file");
        }

        var recipients = new List<CampaignRecipientDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ImportRowError>();
        var invalid = 0;
        var duplicates = 0;

        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        {
            var header = await reader.ReadLineAsync(ct);
            if (header is null)
            {
                return Problem(statusCode: 400, title: "CSV has no header row");
            }

            var columns = CampaignManager.Application.Imports.CsvLine.Split(header);
            var addressIndex = Array.FindIndex(columns,
                c => string.Equals(c, "address", StringComparison.OrdinalIgnoreCase));
            if (addressIndex < 0) addressIndex = 0;

            var line = 1;
            while (await reader.ReadLineAsync(ct) is { } row)
            {
                line++;
                if (string.IsNullOrWhiteSpace(row)) continue;
                if (recipients.Count >= CreateCampaignValidator.MaxRecipients)
                {
                    return Problem(statusCode: 400,
                        title: $"CSV exceeds the maximum of {CreateCampaignValidator.MaxRecipients:N0} recipients");
                }

                var fields = CampaignManager.Application.Imports.CsvLine.Split(row);
                var address = addressIndex < fields.Length ? fields[addressIndex].Trim() : string.Empty;
                if (address.Length is 0 or > 320)
                {
                    invalid++;
                    if (errors.Count < MaxReportedErrors)
                    {
                        errors.Add(new ImportRowError(line, address.Length == 0 ? "Missing address" : "Address too long"));
                    }

                    continue;
                }

                if (!seen.Add(address))
                {
                    duplicates++;
                    continue;
                }

                Dictionary<string, string>? personalization = null;
                for (var i = 0; i < columns.Length && i < fields.Length; i++)
                {
                    if (i == addressIndex || string.IsNullOrWhiteSpace(columns[i])) continue;
                    (personalization ??= [])[columns[i].Trim()] = fields[i].Trim();
                }

                recipients.Add(new CampaignRecipientDto { Address = address, Personalization = personalization });
            }
        }

        if (recipients.Count == 0)
        {
            return Problem(statusCode: 400, title: "No valid recipients in CSV",
                detail: $"{invalid} invalid rows, {duplicates} duplicates.");
        }

        var response = await _sender.Send(new CreateCampaignCommand(new CreateCampaignRequest
        {
            Name = name,
            Channel = channel,
            Sender = sender,
            Subject = subject,
            MessageBody = messageBody,
            TemplateId = templateId,
            ScheduledAtUtc = scheduledAtUtc,
            CallbackUrl = callbackUrl,
            Recipients = recipients
        }), ct);

        return AcceptedAtAction(
            "GetById", "Campaigns", new { campaignId = response.CampaignId },
            new ImportResult(response.CampaignId, response.TrackingId, response.Status,
                recipients.Count, invalid, duplicates, errors));
    }

}
