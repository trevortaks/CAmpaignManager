using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CampaignManager.Infrastructure.Identity;

/// <summary>Adds the organization claim to cookie-authenticated principals so the
/// AdminUI resolves the same tenant context as JWT/API-key callers.</summary>
public sealed class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(JwtTokenService.OrganizationClaim, user.OrganizationId.ToString()));
        return identity;
    }
}
