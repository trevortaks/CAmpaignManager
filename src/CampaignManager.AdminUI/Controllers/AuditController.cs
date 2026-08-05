using CampaignManager.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AuditController : Controller
{
    private readonly IAppDbContext _db;

    public AuditController(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(
        string? entityType, string? action, int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 50;
        var logs = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) logs = logs.Where(l => l.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) logs = logs.Where(l => l.Action == action);

        ViewData["EntityType"] = entityType;
        ViewData["Action"] = action;
        ViewData["Page"] = page;
        return View(await logs
            .OrderByDescending(l => l.TimestampUtc)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct));
    }
}
