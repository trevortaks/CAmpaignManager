namespace CampaignManager.Application.Abstractions;

/// <summary>Ambient tenant for the current unit of work. In HTTP requests it is resolved from
/// auth claims; in background jobs it must be set explicitly from job arguments.</summary>
public interface ICurrentTenant
{
    Guid? OrganizationId { get; }
    Guid? UserId { get; }
    string? IpAddress { get; }
}

/// <summary>Settable variant used by background jobs to establish tenant scope.</summary>
public interface ITenantSetter
{
    void Set(Guid organizationId, Guid? userId = null, string? ipAddress = null);
}
