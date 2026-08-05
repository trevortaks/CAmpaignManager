using CampaignManager.Application.Admin.ApiKeys;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ApiKeysController : Controller
{
    private readonly ISender _sender;

    public ApiKeysController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _sender.Send(new ListApiKeysQuery(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, DateTime? expiresAtUtc, CancellationToken ct)
    {
        var (_, plaintext) = await _sender.Send(new CreateApiKeyCommand(name, expiresAtUtc), ct);
        // Shown exactly once; only the hash is stored.
        TempData["NewKey"] = plaintext;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _sender.Send(new RevokeApiKeyCommand(id), ct);
        TempData["Message"] = "API key revoked.";
        return RedirectToAction(nameof(Index));
    }
}
