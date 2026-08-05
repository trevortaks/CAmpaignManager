using CampaignManager.Application.Campaigns.Queries.GetCampaignStatus;
using CampaignManager.Application.Campaigns.Queries.SearchCampaigns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize]
public sealed class CampaignsController : Controller
{
    private readonly ISender _sender;

    public CampaignsController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(
        string? search, string? status, string? channel, int page = 1, CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new SearchCampaignsQuery(search, status, channel, null, null, page, 20), ct);
        ViewData["Search"] = search;
        ViewData["Status"] = status;
        ViewData["Channel"] = channel;
        return View(result);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct = default)
    {
        var status = await _sender.Send(new GetCampaignStatusQuery(id, null), ct);
        return View(status);
    }
}
