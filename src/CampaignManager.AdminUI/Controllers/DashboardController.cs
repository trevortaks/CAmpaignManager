using CampaignManager.Application.Admin.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _sender.Send(new GetDashboardQuery(), ct));
}
