using CampaignManager.Application.Compliance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.Api.Controllers;

[ApiController]
[Route("api/compliance")]
[Authorize(Policy = "ApiAccess")]
public sealed class ComplianceController : ControllerBase
{
    private readonly ISender _sender;

    public ComplianceController(ISender sender)
    {
        _sender = sender;
    }

    public sealed record SuppressRequest(string Address, string? Channel, string? Reason);

    [HttpGet("suppressions")]
    public async Task<IReadOnlyList<SuppressionSummary>> ListSuppressions(CancellationToken ct) =>
        await _sender.Send(new ListSuppressionsQuery(), ct);

    [HttpPost("suppressions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Suppress([FromBody] SuppressRequest request, CancellationToken ct)
    {
        var id = await _sender.Send(new AddSuppressionCommand(request.Address, request.Channel, request.Reason ?? "opt-out"), ct);
        return CreatedAtAction(nameof(ListSuppressions), new { }, new { suppressionId = id });
    }

    [HttpDelete("suppressions/{id:guid}")]
    public async Task<IActionResult> RemoveSuppression(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteSuppressionCommand(id), ct);
        return NoContent();
    }

    public sealed record EraseRequest(string Address);

    /// <summary>Right-to-erasure: redacts every stored copy of this address's PII in the
    /// tenant's campaign/series recipient lists. Does not remove Suppression rows — an
    /// erased address should still never be re-contacted.</summary>
    [HttpPost("erase")]
    public async Task<object> Erase([FromBody] EraseRequest request, CancellationToken ct)
    {
        var rowsRedacted = await _sender.Send(new EraseRecipientCommand(request.Address), ct);
        return new { rowsRedacted };
    }
}
