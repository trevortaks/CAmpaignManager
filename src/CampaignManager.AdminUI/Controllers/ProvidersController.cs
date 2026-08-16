using CampaignManager.AdminUI.Models;
using CampaignManager.Application.Admin.Providers;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ProvidersController(ISender sender) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await sender.Send(new ListProvidersQuery(), ct));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        if (id is null) return View(new ProviderEditModel());
        var detail = await sender.Send(new GetProviderQuery(id.Value), ct);
        return View(new ProviderEditModel
        {
            Id = detail.Id,
            Channel = detail.Channel,
            ProviderKey = detail.ProviderKey,
            Name = detail.Name,
            Priority = detail.Priority,
            IsEnabled = detail.IsEnabled,
            RateLimitPerMinute = detail.RateLimitPerMinute,
            MaxRetries = detail.MaxRetries,
            RetryDelaySeconds = detail.RetryDelaySeconds,
            Settings = new Dictionary<string, string>(detail.Settings),
            CredentialKeys = detail.CredentialKeys,
            HasWebhookSecret = detail.HasWebhookSecret,
            LastTestedAtUtc = detail.LastTestedAtUtc,
            LastTestSucceeded = detail.LastTestSucceeded
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProviderEditModel model, CancellationToken ct)
    {
        ProviderDefinition? definition = null;
        if (!Enum.TryParse<Channel>(model.Channel, true, out var channel))
            ModelState.AddModelError(nameof(model.Channel), "Select a valid channel.");
        else if ((definition = ProviderCatalog.Find(channel, model.ProviderKey)) is null)
            ModelState.AddModelError(nameof(model.ProviderKey), "Select a provider available for this channel.");

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        if (model.Priority < 1) ModelState.AddModelError(nameof(model.Priority), "Failover position must be at least 1.");
        if (model.RateLimitPerMinute is <= 0) ModelState.AddModelError(nameof(model.RateLimitPerMinute), "Rate limit must be positive when set.");
        if (model.MaxRetries is < 0 or > 5) ModelState.AddModelError(nameof(model.MaxRetries), "Max retries must be between 0 and 5.");
        if (model.RetryDelaySeconds is < 1 or > 300) ModelState.AddModelError(nameof(model.RetryDelaySeconds), "Retry delay must be between 1 and 300 seconds.");

        if (definition is not null)
        {
            model.Settings = ProviderCatalog.ApplyDefaults(definition, model.Settings);
            foreach (var error in ProviderCatalog.ValidateSettings(definition, model.Settings))
                ModelState.AddModelError(error.Key, error.Value);

            var availableCredentialKeys = model.Credentials.Keys.AsEnumerable();
            if (model.CredentialsAction == SecretUpdateAction.Keep && model.Id is { } id)
            {
                var current = await sender.Send(new GetProviderQuery(id), ct);
                availableCredentialKeys = current.CredentialKeys;
                model.CredentialKeys = current.CredentialKeys;
                model.HasWebhookSecret = current.HasWebhookSecret;
                model.IsEnabled = current.IsEnabled;
                model.LastTestedAtUtc = current.LastTestedAtUtc;
                model.LastTestSucceeded = current.LastTestSucceeded;
            }
            if (model.CredentialsAction != SecretUpdateAction.Clear)
                foreach (var error in model.CredentialsAction == SecretUpdateAction.Replace
                             ? ProviderCatalog.ValidateSecrets(definition, model.Credentials)
                             : ProviderCatalog.ValidateSecrets(definition, availableCredentialKeys))
                    ModelState.AddModelError(error.Key, error.Value);
            if (model.WebhookSecretAction == SecretUpdateAction.Replace && string.IsNullOrWhiteSpace(model.WebhookSecret))
                ModelState.AddModelError(nameof(model.WebhookSecret), "Enter the replacement webhook secret.");
        }

        if (!ModelState.IsValid) return View(nameof(Edit), model);

        try
        {
            await sender.Send(new SaveProviderCommand(new SaveProviderInput
            {
                Id = model.Id,
                Channel = model.Channel,
                ProviderKey = model.ProviderKey,
                Name = model.Name,
                Priority = model.Priority,
                RateLimitPerMinute = model.RateLimitPerMinute,
                MaxRetries = model.MaxRetries,
                RetryDelaySeconds = model.RetryDelaySeconds,
                Settings = model.Settings,
                Credentials = model.Credentials,
                CredentialsAction = model.CredentialsAction,
                WebhookSecret = model.WebhookSecret,
                WebhookSecretAction = model.WebhookSecretAction
            }), ct);
            TempData["Message"] = model.Id is null
                ? "Provider saved disabled. Test it, then enable it."
                : "Provider saved. Connection changes disable it and require a new test.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is Domain.Exceptions.DomainException
                                       or Application.Exceptions.NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Edit), model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new TestProviderCommand(id), ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success
            ? "Connection test succeeded. You may now enable the provider."
            : $"Connection test failed: {result.ErrorCode}: {result.ErrorMessage}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(Guid id, bool enabled, bool confirmUntested, CancellationToken ct)
    {
        try
        {
            await sender.Send(new SetProviderEnabledCommand(id, enabled, confirmUntested), ct);
            TempData["Message"] = enabled ? "Provider enabled." : "Provider disabled.";
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new DeleteProviderCommand(id), ct);
            TempData["Message"] = "Provider deleted.";
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
