using CampaignManager.Application.Abstractions;
using CampaignManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UsersController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentTenant _tenant;

    public UsersController(UserManager<AppUser> userManager, ICurrentTenant tenant)
    {
        _userManager = userManager;
        _tenant = tenant;
    }

    public sealed record UserRow(Guid Id, string? Email, string? DisplayName, IList<string> Roles, bool LockedOut);

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users
            .Where(u => u.OrganizationId == _tenant.OrganizationId)
            .OrderBy(u => u.Email)
            .ToList();
        var rows = new List<UserRow>();
        foreach (var user in users)
        {
            rows.Add(new UserRow(user.Id, user.Email, user.DisplayName,
                await _userManager.GetRolesAsync(user),
                await _userManager.IsLockedOutAsync(user)));
        }

        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string displayName, string password, string role)
    {
        if (_tenant.OrganizationId is not { } organizationId) return Forbid();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            OrganizationId = organizationId
        };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        if (role is "Admin" or "Operator" or "Viewer")
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        TempData["Message"] = $"User {email} created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(Guid id, string role)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.OrganizationId != _tenant.OrganizationId) return NotFound();
        var current = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, current);
        if (role is "Admin" or "Operator" or "Viewer")
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        TempData["Message"] = $"Role updated for {user.Email}.";
        return RedirectToAction(nameof(Index));
    }
}
