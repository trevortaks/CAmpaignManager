using System.Security.Cryptography;
using System.Text;
using CampaignManager.Application.Abstractions;
using CampaignManager.Contracts.Webhooks;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Api.Controllers;

/// <summary>Provider delivery callbacks. Anonymous (providers can't do JWT) but each call must
/// carry the provider configuration's webhook secret in X-Webhook-Secret.</summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController : ControllerBase
{
    public const string SecretHeader = "X-Webhook-Secret";

    private readonly IAppDbContext _db;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IAppDbContext db, ILogger<WebhooksController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("{providerKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Receive(
        string providerKey, [FromBody] ProviderCallbackRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(SecretHeader, out var presentedSecret))
        {
            return Unauthorized();
        }

        // Webhooks arrive unauthenticated, so tenant filters don't apply; correlate by
        // provider message id and verify the secret of the matching configuration.
        var message = await _db.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.ProviderMessageId == request.ProviderMessageId, ct);
        if (message is null)
        {
            _logger.LogWarning("Webhook for unknown provider message id {ProviderMessageId} via {ProviderKey}",
                request.ProviderMessageId, providerKey);
            return NotFound();
        }

        var configSecret = await _db.ProviderConfigurations
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == message.OrganizationId && p.ProviderKey == providerKey)
            .Select(p => p.WebhookSecret)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(configSecret) || !FixedTimeEquals(configSecret, presentedSecret.ToString()))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<MessageStatus>(request.Status, ignoreCase: true, out var reportedStatus))
        {
            return BadRequest();
        }

        var utcNow = request.OccurredAtUtc ?? DateTime.UtcNow;
        if (message.TryApplyWebhookStatus(reportedStatus, utcNow))
        {
            _db.DeliveryEvents.Add(new DeliveryEvent
            {
                MessageId = message.Id,
                Status = reportedStatus,
                Detail = request.Detail,
                OccurredAtUtc = utcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private static bool FixedTimeEquals(string expected, string presented) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)));
}
