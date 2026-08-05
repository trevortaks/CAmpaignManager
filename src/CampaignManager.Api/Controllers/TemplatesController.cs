using CampaignManager.Application.Admin.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.Api.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize(Policy = "ApiAccess")]
public sealed class TemplatesController : ControllerBase
{
    private readonly ISender _sender;

    public TemplatesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IReadOnlyList<TemplateSummary>> List(CancellationToken ct) =>
        await _sender.Send(new ListTemplatesQuery(), ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveTemplateInput input, CancellationToken ct)
    {
        var id = await _sender.Send(new SaveTemplateCommand(input), ct);
        return CreatedAtAction(nameof(List), new { }, new { templateId = id });
    }

    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> Update(Guid templateId, [FromBody] SaveTemplateInput input, CancellationToken ct)
    {
        await _sender.Send(new SaveTemplateCommand(new SaveTemplateInput
        {
            Id = templateId,
            Name = input.Name,
            Channel = input.Channel,
            Subject = input.Subject,
            Body = input.Body,
            IsActive = input.IsActive
        }), ct);
        return NoContent();
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Delete(Guid templateId, CancellationToken ct)
    {
        await _sender.Send(new DeleteTemplateCommand(templateId), ct);
        return NoContent();
    }

    public sealed record PreviewRequest(
        Guid? TemplateId, string? Body, string? Subject, Dictionary<string, string>? SampleData);

    /// <summary>Renders a template (stored or ad-hoc body) against sample data.</summary>
    [HttpPost("preview")]
    public async Task<TemplatePreview> Preview([FromBody] PreviewRequest request, CancellationToken ct) =>
        await _sender.Send(new PreviewTemplateQuery(
            request.TemplateId, request.Body, request.Subject, request.SampleData ?? []), ct);
}
