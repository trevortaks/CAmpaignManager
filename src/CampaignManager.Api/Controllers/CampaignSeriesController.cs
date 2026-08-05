using CampaignManager.Application.Campaigns.Series;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampaignManager.Api.Controllers;

/// <summary>Recurring campaign definitions. Each cron occurrence materializes a full
/// Campaign instance through the normal create pipeline (see CampaignSeriesJob).</summary>
[ApiController]
[Route("api/campaign-series")]
[Authorize(Policy = "ApiAccess")]
[EnableRateLimiting("campaigns")]
public sealed class CampaignSeriesController : ControllerBase
{
    private readonly ISender _sender;

    public CampaignSeriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IReadOnlyList<CampaignSeriesSummary>> List(CancellationToken ct) =>
        await _sender.Send(new ListCampaignSeriesQuery(), ct);

    [HttpGet("{seriesId:guid}")]
    public async Task<SaveCampaignSeriesInput> GetById(Guid seriesId, CancellationToken ct) =>
        await _sender.Send(new GetCampaignSeriesQuery(seriesId), ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveCampaignSeriesInput input, CancellationToken ct)
    {
        var id = await _sender.Send(new SaveCampaignSeriesCommand(input), ct);
        return CreatedAtAction(nameof(GetById), new { seriesId = id }, new { seriesId = id });
    }

    [HttpPut("{seriesId:guid}")]
    public async Task<IActionResult> Update(
        Guid seriesId, [FromBody] SaveCampaignSeriesInput input, CancellationToken ct)
    {
        await _sender.Send(new SaveCampaignSeriesCommand(new SaveCampaignSeriesInput
        {
            Id = seriesId,
            Name = input.Name,
            Channel = input.Channel,
            Sender = input.Sender,
            Subject = input.Subject,
            MessageBody = input.MessageBody,
            TemplateId = input.TemplateId,
            CallbackUrl = input.CallbackUrl,
            Priority = input.Priority,
            CronExpression = input.CronExpression,
            IsActive = input.IsActive,
            Recipients = input.Recipients
        }), ct);
        return NoContent();
    }

    [HttpPost("{seriesId:guid}/pause")]
    public async Task<IActionResult> Pause(Guid seriesId, CancellationToken ct)
    {
        await _sender.Send(new SetCampaignSeriesActiveCommand(seriesId, false), ct);
        return NoContent();
    }

    [HttpPost("{seriesId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid seriesId, CancellationToken ct)
    {
        await _sender.Send(new SetCampaignSeriesActiveCommand(seriesId, true), ct);
        return NoContent();
    }

    [HttpDelete("{seriesId:guid}")]
    public async Task<IActionResult> Delete(Guid seriesId, CancellationToken ct)
    {
        await _sender.Send(new DeleteCampaignSeriesCommand(seriesId), ct);
        return NoContent();
    }
}
