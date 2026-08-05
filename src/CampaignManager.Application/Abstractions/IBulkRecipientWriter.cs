using CampaignManager.Contracts.Campaigns;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Application.Abstractions;

/// <summary>High-throughput recipient/message insertion via SqlBulkCopy, bypassing EF change
/// tracking. Used for large campaigns (see CreateCampaignHandler.BulkCopyThreshold) and by
/// the dispatch job's lazy message-creation path for campaigns whose recipients were bulk
/// inserted without pre-created messages.</summary>
public interface IBulkRecipientWriter
{
    /// <summary>Bulk-inserts recipients and returns their generated ids in insertion order.</summary>
    Task<IReadOnlyList<long>> BulkInsertRecipientsAsync(
        Guid organizationId, Guid campaignId, IReadOnlyList<CampaignRecipientDto> recipients,
        CancellationToken ct);

    /// <summary>Bulk-inserts one Queued Message row per recipient id.</summary>
    Task BulkInsertMessagesAsync(
        Guid organizationId, Guid campaignId, Channel channel, IReadOnlyList<long> recipientIds,
        CancellationToken ct);
}
