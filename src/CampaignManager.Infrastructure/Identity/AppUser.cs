using Microsoft.AspNetCore.Identity;

namespace CampaignManager.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }
    public string? DisplayName { get; set; }
}
