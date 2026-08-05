using CampaignManager.Application.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin")]
public sealed class NotificationsController : Controller
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Log = await _sender.Send(new ListNotificationLogQuery(), ct);
        return View(await _sender.Send(new GetNotificationSettingsQuery(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        string? recipientEmail, bool notifyProviderOffline, bool notifyCampaignCompleted,
        bool notifyCampaignFailed, double highFailureRateThreshold, CancellationToken ct)
    {
        await _sender.Send(new SaveNotificationSettingsCommand(new NotificationSettingsDto(
            recipientEmail, notifyProviderOffline, notifyCampaignCompleted, notifyCampaignFailed,
            highFailureRateThreshold / 100.0)), ct);
        TempData["Message"] = "Notification settings saved.";
        return RedirectToAction(nameof(Index));
    }
}
