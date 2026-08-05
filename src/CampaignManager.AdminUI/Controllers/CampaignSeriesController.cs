using CampaignManager.Application.Campaigns.Series;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin,Operator")]
public sealed class CampaignSeriesController : Controller
{
    private readonly ISender _sender;

    public CampaignSeriesController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _sender.Send(new ListCampaignSeriesQuery(), ct));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        if (id is null) return View(model: null);
        return View(await _sender.Send(new GetCampaignSeriesQuery(id.Value), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        Guid? id, string name, string channel, string sender, string? subject, string? messageBody,
        string cronExpression, int priority, bool isActive, string recipientsRaw, CancellationToken ct)
    {
        try
        {
            var input = new SaveCampaignSeriesInput
            {
                Id = id,
                Name = name,
                Channel = channel,
                Sender = sender,
                Subject = subject,
                MessageBody = messageBody,
                CronExpression = cronExpression,
                Priority = priority,
                IsActive = isActive,
                Recipients = ParseRecipients(recipientsRaw)
            };
            await _sender.Send(new SaveCampaignSeriesCommand(input), ct);
            TempData["Message"] = "Recurring campaign saved.";
        }
        catch (Exception ex) when (ex is Domain.Exceptions.DomainException
                                       or Application.Exceptions.NotFoundException
                                       or FluentValidation.ValidationException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, bool active, CancellationToken ct)
    {
        await _sender.Send(new SetCampaignSeriesActiveCommand(id, active), ct);
        TempData["Message"] = active ? "Resumed." : "Paused.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteCampaignSeriesCommand(id), ct);
        TempData["Message"] = "Recurring campaign deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Parses "address,FirstName=Ada,Date=Friday" — one recipient per line.</summary>
    private static List<SeriesRecipientInput> ParseRecipients(string? raw)
    {
        var recipients = new List<SeriesRecipientInput>();
        if (string.IsNullOrWhiteSpace(raw)) return recipients;
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(',');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) continue;
            Dictionary<string, string>? personalization = null;
            for (var i = 1; i < parts.Length; i++)
            {
                var separator = parts[i].IndexOf('=');
                if (separator > 0)
                {
                    (personalization ??= [])[parts[i][..separator].Trim()] = parts[i][(separator + 1)..].Trim();
                }
            }

            recipients.Add(new SeriesRecipientInput(parts[0].Trim(), personalization));
        }

        return recipients;
    }
}
