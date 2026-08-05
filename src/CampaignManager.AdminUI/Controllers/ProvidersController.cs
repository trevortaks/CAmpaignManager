using CampaignManager.Application.Admin.Providers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ProvidersController : Controller
{
    private readonly ISender _sender;

    public ProvidersController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _sender.Send(new ListProvidersQuery(), ct));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        if (id is null) return View(model: null);
        return View(await _sender.Send(new GetProviderQuery(id.Value), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        Guid? id, string channel, string providerKey, string name, int priority, bool isEnabled,
        int? rateLimitPerMinute, int maxRetries, int retryDelaySeconds,
        string? settingsRaw, string? credentialsRaw, string? webhookSecret,
        CancellationToken ct)
    {
        try
        {
            await _sender.Send(new SaveProviderCommand(new SaveProviderInput
            {
                Id = id,
                Channel = channel,
                ProviderKey = providerKey,
                Name = name,
                Priority = priority,
                IsEnabled = isEnabled,
                RateLimitPerMinute = rateLimitPerMinute,
                MaxRetries = maxRetries,
                RetryDelaySeconds = retryDelaySeconds,
                Settings = ParseKeyValues(settingsRaw),
                Credentials = ParseKeyValues(credentialsRaw),
                WebhookSecret = string.IsNullOrWhiteSpace(webhookSecret) ? null : webhookSecret
            }), ct);
            TempData["Message"] = "Provider saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is Domain.Exceptions.DomainException
                                       or Application.Exceptions.NotFoundException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new TestProviderCommand(id), ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success
            ? "Connection test succeeded."
            : $"Connection test failed: {result.ErrorCode}: {result.ErrorMessage}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(Guid id, bool enabled, CancellationToken ct)
    {
        await _sender.Send(new SetProviderEnabledCommand(id, enabled), ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _sender.Send(new DeleteProviderCommand(id), ct);
            TempData["Message"] = "Provider deleted.";
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Parses "key=value" lines from a textarea into a dictionary.</summary>
    private static Dictionary<string, string> ParseKeyValues(string? raw)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return result;
    }
}
