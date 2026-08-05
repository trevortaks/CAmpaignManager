using CampaignManager.Application.Campaigns.Commands.CancelCampaign;
using CampaignManager.Application.Campaigns.Commands.CreateCampaign;
using CampaignManager.Application.Campaigns.Queries.GetCampaignStatus;
using CampaignManager.Application.Campaigns.Queries.SearchCampaigns;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize(Policy = "ApiAccess")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("campaigns")]
public sealed class CampaignsController : ControllerBase
{
    private readonly ISender _sender;

    public CampaignsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Submits a campaign. Validation is synchronous; sending happens in the background.</summary>
    [HttpPost]
    [ProducesResponseType<CreateCampaignResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var response = await _sender.Send(new CreateCampaignCommand(request), ct);
        return AcceptedAtAction(nameof(GetById), new { campaignId = response.CampaignId }, response);
    }

    [HttpGet("{campaignId:guid}")]
    [ProducesResponseType<CampaignStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<CampaignStatusResponse> GetById(Guid campaignId, CancellationToken ct) =>
        await _sender.Send(new GetCampaignStatusQuery(campaignId, null), ct);

    [HttpGet("by-tracking/{trackingId}")]
    [ProducesResponseType<CampaignStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<CampaignStatusResponse> GetByTrackingId(string trackingId, CancellationToken ct) =>
        await _sender.Send(new GetCampaignStatusQuery(null, trackingId), ct);

    [HttpPost("{campaignId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid campaignId, CancellationToken ct)
    {
        await _sender.Send(new CancelCampaignCommand(campaignId), ct);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<CampaignSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<CampaignSummaryResponse>> Search(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? channel,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        await _sender.Send(new SearchCampaignsQuery(search, status, channel, fromUtc, toUtc, page, pageSize), ct);
}
