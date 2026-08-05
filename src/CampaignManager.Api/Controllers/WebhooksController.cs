using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Contracts.Webhooks;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Api.Controllers;

/// <summary>Provider delivery callbacks. Anonymous (providers can't do JWT); authenticated by
/// either the configuration's shared secret (X-Webhook-Secret) or a provider-specific
/// signature over the raw body (X-Hub-Signature-256 for Meta, X-Twilio-Signature for Twilio).
/// Callbacks that reference an unknown provider message id are dead-lettered and replayed
/// by a recurring job.</summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
[EnableRateLimiting("webhooks")]
public sealed class WebhooksController : ControllerBase
{
    public const string SecretHeader = "X-Webhook-Secret";

    private static readonly Dictionary<string, string> SignatureHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["meta-whatsapp"] = "X-Hub-Signature-256",
        ["twilio"] = "X-Twilio-Signature"
    };

    private readonly IAppDbContext _db;
    private readonly IEnumerable<IWebhookSignatureVerifier> _verifiers;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IAppDbContext db,
        IEnumerable<IWebhookSignatureVerifier> verifiers,
        ILogger<WebhooksController> logger)
    {
        _db = db;
        _verifiers = verifiers;
        _logger = logger;
    }

    [HttpPost("{providerKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(string providerKey, CancellationToken ct)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync(ct);
        }

        ProviderCallbackRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ProviderCallbackRequest>(rawBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (payload is null || string.IsNullOrEmpty(payload.ProviderMessageId))
        {
            return BadRequest();
        }

        // Load candidate secrets for this provider key across organizations; the caller is
        // unauthenticated so tenant filters don't apply.
        var secrets = await _db.ProviderConfigurations
            .IgnoreQueryFilters()
            .Where(p => p.ProviderKey == providerKey && p.WebhookSecret != null)
            .Select(p => p.WebhookSecret!)
            .Distinct()
            .ToListAsync(ct);

        if (!IsAuthentic(providerKey, rawBody, secrets))
        {
            return Unauthorized();
        }

        CampaignManager.Application.Observability.CampaignMetrics.WebhooksReceived.Add(1,
            new KeyValuePair<string, object?>("provider", providerKey));

        if (!Enum.TryParse<MessageStatus>(payload.Status, ignoreCase: true, out var reportedStatus))
        {
            return BadRequest();
        }

        var message = await _db.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.ProviderMessageId == payload.ProviderMessageId, ct);

        var utcNow = payload.OccurredAtUtc ?? DateTime.UtcNow;
        if (message is null)
        {
            // Likely a race with the send batch persisting ProviderMessageId — dead-letter for replay.
            _db.WebhookDeadLetters.Add(new WebhookDeadLetter
            {
                ProviderKey = providerKey,
                ProviderMessageId = payload.ProviderMessageId,
                ReportedStatus = payload.Status,
                Detail = payload.Detail,
                OccurredAtUtc = payload.OccurredAtUtc,
                ReceivedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Dead-lettered webhook for unknown provider message id {ProviderMessageId}",
                payload.ProviderMessageId);
            return Accepted();
        }

        if (message.TryApplyWebhookStatus(reportedStatus, utcNow))
        {
            _db.DeliveryEvents.Add(new DeliveryEvent
            {
                MessageId = message.Id,
                Status = reportedStatus,
                Detail = payload.Detail,
                OccurredAtUtc = utcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private bool IsAuthentic(string providerKey, string rawBody, List<string> secrets)
    {
        if (secrets.Count == 0) return false;

        if (Request.Headers.TryGetValue(SecretHeader, out var presentedSecret))
        {
            var presented = presentedSecret.ToString();
            return secrets.Any(secret => FixedTimeEquals(secret, presented));
        }

        if (SignatureHeaders.TryGetValue(providerKey, out var headerName) &&
            Request.Headers.TryGetValue(headerName, out var signature))
        {
            var verifier = _verifiers.FirstOrDefault(v =>
                string.Equals(v.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));
            if (verifier is null) return false;
            var requestUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
            return secrets.Any(secret => verifier.Verify(secret, rawBody, signature.ToString(), requestUrl));
        }

        return false;
    }

    private static bool FixedTimeEquals(string expected, string presented) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)));
}
