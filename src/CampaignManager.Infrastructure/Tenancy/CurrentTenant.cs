using CampaignManager.Application.Abstractions;

namespace CampaignManager.Infrastructure.Tenancy;

/// <summary>Scoped tenant holder. HTTP hosts populate it from auth claims via middleware;
/// background jobs populate it from job arguments. Never resolved from IHttpContextAccessor
/// directly so the same type works in both hosts.</summary>
public sealed class CurrentTenant : ICurrentTenant, ITenantSetter
{
    public Guid? OrganizationId { get; private set; }
    public Guid? UserId { get; private set; }

    public void Set(Guid organizationId, Guid? userId = null)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }
}
