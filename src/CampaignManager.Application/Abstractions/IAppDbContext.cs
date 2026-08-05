using CampaignManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<CampaignRecipient> CampaignRecipients { get; }
    DbSet<Message> Messages { get; }
    DbSet<DeliveryEvent> DeliveryEvents { get; }
    DbSet<ProviderConfiguration> ProviderConfigurations { get; }
    DbSet<MessageTemplate> MessageTemplates { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<WebhookDeadLetter> WebhookDeadLetters { get; }
    DbSet<DailyStatistic> DailyStatistics { get; }
    DbSet<CampaignSeries> CampaignSeries { get; }
    DbSet<CampaignSeriesRecipient> CampaignSeriesRecipients { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<NotificationSettings> NotificationSettings { get; }
    DbSet<Suppression> Suppressions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
