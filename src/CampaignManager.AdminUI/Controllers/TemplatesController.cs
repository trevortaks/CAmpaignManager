using CampaignManager.Application.Admin.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin,Operator")]
public sealed class TemplatesController : Controller
{
    private readonly ISender _sender;

    public TemplatesController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _sender.Send(new ListTemplatesQuery(), ct));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        if (id is null) return View(model: null);
        var templates = await _sender.Send(new ListTemplatesQuery(), ct);
        var template = templates.FirstOrDefault(t => t.Id == id.Value);
        if (template is null) return NotFound();
        return View(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        Guid? id, string name, string channel, string? subject, string body, bool isActive,
        CancellationToken ct)
    {
        await _sender.Send(new SaveTemplateCommand(new SaveTemplateInput
        {
            Id = id, Name = name, Channel = channel, Subject = subject, Body = body, IsActive = isActive
        }), ct);
        TempData["Message"] = "Template saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteTemplateCommand(id), ct);
        TempData["Message"] = "Template deleted (or deactivated if in use).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Ajax preview used by the edit page.</summary>
    [HttpPost]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewInput input, CancellationToken ct) =>
        Json(await _sender.Send(new PreviewTemplateQuery(
            null, input.Body, input.Subject, input.SampleData ?? []), ct));

    public sealed record PreviewInput(string? Body, string? Subject, Dictionary<string, string>? SampleData);
}
