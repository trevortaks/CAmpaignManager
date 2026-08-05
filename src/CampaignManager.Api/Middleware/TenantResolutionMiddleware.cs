using CampaignManager.Application.Abstractions;
using CampaignManager.Infrastructure.Identity;

namespace CampaignManager.Api.Middleware;

/// <summary>Populates the scoped CurrentTenant from the authenticated principal's "org" claim.
/// Must run after UseAuthentication and before any handler that touches tenant-filtered data.</summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantSetter tenantSetter)
    {
        var orgClaim = context.User.FindFirst(JwtTokenService.OrganizationClaim)?.Value;
        if (Guid.TryParse(orgClaim, out var organizationId))
        {
            var userClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? context.User.FindFirst("sub")?.Value;
            Guid.TryParse(userClaim, out var userId);
            tenantSetter.Set(organizationId, userId == Guid.Empty ? null : userId);
        }

        await _next(context);
    }
}
