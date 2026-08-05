using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CampaignManager.Infrastructure.Persistence.Interceptors;

/// <summary>Writes AuditLog rows for mutations of admin-managed entities in the same
/// SaveChanges call. High-volume entities (Message, DeliveryEvent, CampaignRecipient,
/// DailyStatistic) are deliberately excluded.</summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(ProviderConfiguration), typeof(MessageTemplate), typeof(ApiKey),
        typeof(Campaign), typeof(Organization)
    ];

    // Never write these property values into the audit trail.
    private static readonly HashSet<string> RedactedProperties =
        ["EncryptedCredentials", "WebhookSecret", "KeyHash"];

    private readonly ICurrentTenant _tenant;

    public AuditSaveChangesInterceptor(ICurrentTenant tenant)
    {
        _tenant = tenant;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AddAuditEntries(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AddAuditEntries(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void AddAuditEntries(DbContext context)
    {
        var utcNow = DateTime.UtcNow;
        List<AuditLog>? logs = null;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (!AuditedTypes.Contains(entry.Entity.GetType())) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var (oldValues, newValues) = CollectValues(entry);
            if (entry.State == EntityState.Modified && newValues is null) continue; // nothing actually changed

            logs ??= [];
            logs.Add(new AuditLog
            {
                OrganizationId = _tenant.OrganizationId,
                UserId = _tenant.UserId,
                Action = entry.State.ToString(),
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Property("Id").CurrentValue?.ToString(),
                IpAddress = _tenant.IpAddress,
                OldValuesJson = oldValues,
                NewValuesJson = newValues,
                TimestampUtc = utcNow
            });
        }

        if (logs is not null)
        {
            context.Set<AuditLog>().AddRange(logs);
        }
    }

    private static (string? OldValues, string? NewValues) CollectValues(EntityEntry entry)
    {
        Dictionary<string, object?>? oldValues = null;
        Dictionary<string, object?>? newValues = null;

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (name == "RowVersion") continue;
            object? Value(object? raw) => RedactedProperties.Contains(name) ? "***" : raw;

            switch (entry.State)
            {
                case EntityState.Added:
                    (newValues ??= []).Add(name, Value(property.CurrentValue));
                    break;
                case EntityState.Deleted:
                    (oldValues ??= []).Add(name, Value(property.OriginalValue));
                    break;
                case EntityState.Modified when property.IsModified &&
                    !Equals(property.OriginalValue, property.CurrentValue):
                    (oldValues ??= []).Add(name, Value(property.OriginalValue));
                    (newValues ??= []).Add(name, Value(property.CurrentValue));
                    break;
            }
        }

        return (oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                newValues is null ? null : JsonSerializer.Serialize(newValues));
    }
}
