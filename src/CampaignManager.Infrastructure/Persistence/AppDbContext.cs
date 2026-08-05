using CampaignManager.Application.Abstractions;
using CampaignManager.Domain.Entities;
using CampaignManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    private readonly ICurrentTenant _currentTenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DeliveryEvent> DeliveryEvents => Set<DeliveryEvent>();
    public DbSet<ProviderConfiguration> ProviderConfigurations => Set<ProviderConfiguration>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WebhookDeadLetter> WebhookDeadLetters => Set<WebhookDeadLetter>();
    public DbSet<DailyStatistic> DailyStatistics => Set<DailyStatistic>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Campaign>(e =>
        {
            e.Property(x => x.TrackingId).HasMaxLength(32);
            e.HasIndex(x => x.TrackingId).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Sender).HasMaxLength(320);
            e.Property(x => x.Subject).HasMaxLength(500);
            e.Property(x => x.ScheduledJobId).HasMaxLength(100);
            e.Property(x => x.CallbackUrl).HasMaxLength(2000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAtUtc }).IsDescending(false, false, true);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x =>
                _currentTenant.OrganizationId == null || x.OrganizationId == _currentTenant.OrganizationId);
        });

        builder.Entity<CampaignRecipient>(e =>
        {
            e.Property(x => x.Address).HasMaxLength(320);
            e.HasIndex(x => new { x.CampaignId, x.Id });
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x =>
                _currentTenant.OrganizationId == null || x.OrganizationId == _currentTenant.OrganizationId);
        });

        builder.Entity<Message>(e =>
        {
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.ProviderMessageId).HasMaxLength(128);
            e.Property(x => x.LastError).HasMaxLength(1024);
            e.HasIndex(x => new { x.CampaignId, x.Status });
            e.HasIndex(x => new { x.OrganizationId, x.QueuedAtUtc });
            e.HasIndex(x => x.ProviderMessageId)
                .HasFilter("[ProviderMessageId] IS NOT NULL");
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasQueryFilter(x =>
                _currentTenant.OrganizationId == null || x.OrganizationId == _currentTenant.OrganizationId);
        });

        builder.Entity<DeliveryEvent>(e =>
        {
            e.Property(x => x.Detail).HasMaxLength(1024);
            e.HasIndex(x => x.MessageId);
            e.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId);
        });

        builder.Entity<ProviderConfiguration>(e =>
        {
            e.Property(x => x.ProviderKey).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.WebhookSecret).HasMaxLength(128);
            e.HasIndex(x => new { x.OrganizationId, x.Channel, x.Priority });
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x =>
                _currentTenant.OrganizationId == null || x.OrganizationId == _currentTenant.OrganizationId);
        });

        builder.Entity<MessageTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Subject).HasMaxLength(500);
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x =>
                _currentTenant.OrganizationId == null || x.OrganizationId == _currentTenant.OrganizationId);
        });

        builder.Entity<ApiKey>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.KeyHash).HasMaxLength(128);
            e.Property(x => x.KeyPrefix).HasMaxLength(12);
            e.HasIndex(x => x.KeyPrefix);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WebhookDeadLetter>(e =>
        {
            e.Property(x => x.ProviderKey).HasMaxLength(50);
            e.Property(x => x.ProviderMessageId).HasMaxLength(128);
            e.Property(x => x.ReportedStatus).HasMaxLength(20);
            e.Property(x => x.Detail).HasMaxLength(1024);
            e.HasIndex(x => x.ReceivedAtUtc)
                .HasFilter("[ResolvedAtUtc] IS NULL AND [AbandonedAtUtc] IS NULL");
        });

        builder.Entity<DailyStatistic>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.Date, x.Channel, x.ProviderConfigurationId })
                .IsUnique();
        });

        builder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => new { x.OrganizationId, x.TimestampUtc });
        });
    }
}
