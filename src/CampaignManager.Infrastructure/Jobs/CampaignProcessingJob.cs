using System.Net.Http.Json;
using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Jobs;
using CampaignManager.Application.Providers;
using CampaignManager.Application.Templating;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.StateMachine;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Jobs;

/// <summary>Executes campaign sending. All work is derived from message Status in the database,
/// so Hangfire's at-least-once execution is safe: re-runs skip rows that already advanced.</summary>
public sealed class CampaignProcessingJob : ICampaignProcessingJob
{
    private const int BatchSize = 500;
    private const int SaveChunkSize = 50;
    private static readonly TimeSpan FinalizerDelay = TimeSpan.FromSeconds(15);

    private readonly IAppDbContext _db;
    private readonly ITenantSetter _tenantSetter;
    private readonly IProviderSelector _providerSelector;
    private readonly FailoverSender _failoverSender;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBulkRecipientWriter _bulkWriter;
    private readonly Application.Notifications.INotificationService _notifications;
    private readonly ILogger<CampaignProcessingJob> _logger;

    public CampaignProcessingJob(
        IAppDbContext db,
        ITenantSetter tenantSetter,
        IProviderSelector providerSelector,
        FailoverSender failoverSender,
        ITemplateRenderer templateRenderer,
        IBackgroundJobClient jobClient,
        IHttpClientFactory httpClientFactory,
        IBulkRecipientWriter bulkWriter,
        Application.Notifications.INotificationService notifications,
        ILogger<CampaignProcessingJob> logger)
    {
        _db = db;
        _tenantSetter = tenantSetter;
        _providerSelector = providerSelector;
        _failoverSender = failoverSender;
        _templateRenderer = templateRenderer;
        _jobClient = jobClient;
        _httpClientFactory = httpClientFactory;
        _bulkWriter = bulkWriter;
        _notifications = notifications;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 600])]
    public async Task DispatchAsync(Guid organizationId, Guid campaignId)
    {
        _tenantSetter.Set(organizationId);
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null || campaign.IsTerminal)
        {
            return;
        }

        if (campaign.Status == CampaignStatus.Scheduled)
        {
            campaign.TransitionTo(CampaignStatus.Queued);
            campaign.ScheduledJobId = null;
        }

        if (campaign.Status == CampaignStatus.Queued)
        {
            campaign.TransitionTo(CampaignStatus.Processing);
            campaign.StartedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        else if (campaign.Status != CampaignStatus.Processing)
        {
            return;
        }

        // Lazy message creation: large campaigns (see CreateCampaignHandler.BulkCopyThreshold)
        // bulk-insert recipients only at creation time and defer Message rows to here, halving
        // the write volume for the common case where a campaign is dispatched immediately.
        var hasMessages = await _db.Messages.AnyAsync(m => m.CampaignId == campaignId);
        if (!hasMessages && campaign.TotalRecipients > 0)
        {
            var recipientIds = await _db.CampaignRecipients
                .Where(r => r.CampaignId == campaignId)
                .OrderBy(r => r.Id)
                .Select(r => r.Id)
                .ToListAsync();
            await _bulkWriter.BulkInsertMessagesAsync(organizationId, campaignId, campaign.Channel, recipientIds, default);
            _logger.LogInformation(
                "Campaign {CampaignId}: lazily bulk-created {Count} Queued messages at dispatch",
                campaignId, recipientIds.Count);
        }

        // Fan out disjoint id ranges; duplicate enqueues are harmless because batch jobs
        // only touch rows still in Queued.
        var queuedIds = await _db.Messages
            .Where(m => m.CampaignId == campaignId && m.Status == MessageStatus.Queued)
            .OrderBy(m => m.Id)
            .Select(m => m.Id)
            .ToListAsync();

        foreach (var chunk in queuedIds.Chunk(BatchSize))
        {
            var first = chunk[0];
            var last = chunk[chunk.Length - 1];
            _jobClient.Enqueue<ICampaignProcessingJob>(
                j => j.SendBatchAsync(organizationId, campaignId, first, last));
        }

        _jobClient.Schedule<ICampaignProcessingJob>(
            j => j.FinalizeAsync(organizationId, campaignId), FinalizerDelay);

        _logger.LogInformation("Dispatched campaign {CampaignId}: {MessageCount} messages in {BatchCount} batches",
            campaignId, queuedIds.Count, (queuedIds.Count + BatchSize - 1) / BatchSize);
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 600])]
    public async Task SendBatchAsync(Guid organizationId, Guid campaignId, long firstMessageId, long lastMessageId)
    {
        _tenantSetter.Set(organizationId);
        var campaign = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null)
        {
            return;
        }

        if (campaign.Status == CampaignStatus.Cancelled)
        {
            await ExpireQueuedInRange(campaignId, firstMessageId, lastMessageId);
            return;
        }

        if (campaign.Status != CampaignStatus.Processing)
        {
            return;
        }

        string? templateBody = null;
        string? templateSubject = null;
        if (campaign.TemplateId is { } templateId)
        {
            var template = await _db.MessageTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId);
            templateBody = template?.Body;
            templateSubject = template?.Subject;
        }

        var bodyTemplate = campaign.MessageBody ?? templateBody ?? string.Empty;
        var subjectTemplate = campaign.Subject ?? templateSubject;

        var providers = await _providerSelector.GetOrderedProvidersAsync(
            organizationId, campaign.Channel, CancellationToken.None);

        var messages = await _db.Messages
            .Include(m => m.Recipient)
            .Where(m => m.CampaignId == campaignId
                        && m.Id >= firstMessageId && m.Id <= lastMessageId
                        && m.Status == MessageStatus.Queued)
            .OrderBy(m => m.Id)
            .ToListAsync();

        // One suppression query per batch (not per message): opted-out/complained addresses
        // must never be sent to again regardless of provider-level consent handling.
        var batchAddresses = messages.Select(m => m.Recipient!.Address.Trim().ToLowerInvariant()).Distinct().ToList();
        var suppressed = (await _db.Suppressions.AsNoTracking()
            .Where(s => batchAddresses.Contains(s.Address) && (s.Channel == null || s.Channel == campaign.Channel))
            .Select(s => s.Address)
            .ToListAsync()).ToHashSet();

        var processedSinceCheck = 0;
        foreach (var message in messages)
        {
            if (suppressed.Contains(message.Recipient!.Address.Trim().ToLowerInvariant()))
            {
                message.LastError = "Suppressed: recipient opted out";
                message.TransitionTo(MessageStatus.Rejected, DateTime.UtcNow);
                _db.DeliveryEvents.Add(new DeliveryEvent
                {
                    MessageId = message.Id, Status = message.Status, Detail = message.LastError, OccurredAtUtc = DateTime.UtcNow
                });
                continue;
            }

            var personalization = Deserialize(message.Recipient?.PersonalizationJson);
            var body = _templateRenderer.Render(bodyTemplate, personalization);
            var subject = subjectTemplate is null ? null : _templateRenderer.Render(subjectTemplate, personalization);
            var utcNow = DateTime.UtcNow;

            message.TransitionTo(MessageStatus.Processing, utcNow);
            message.AttemptCount++;

            var sendStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var (result, providerConfigId) = await _failoverSender.SendAsync(
                providers,
                new ProviderSendRequest(
                    message.PublicId,
                    message.Recipient!.Address,
                    body,
                    subject,
                    campaign.Sender,
                    new Dictionary<string, string>()),
                CancellationToken.None);
            sendStopwatch.Stop();
            var channelTag = new KeyValuePair<string, object?>("channel", campaign.Channel.ToString());
            Application.Observability.CampaignMetrics.ProviderSendDuration.Record(
                sendStopwatch.Elapsed.TotalMilliseconds, channelTag);

            message.ProviderConfigurationId = providerConfigId;
            utcNow = DateTime.UtcNow;
            if (result.Success)
            {
                message.ProviderMessageId = result.ProviderMessageId;
                message.TransitionTo(MessageStatus.Sent, utcNow);
                Application.Observability.CampaignMetrics.MessagesSent.Add(1, channelTag);
            }
            else
            {
                message.LastError = Truncate($"{result.ErrorCode}: {result.ErrorMessage}", 1024);
                message.TransitionTo(
                    result.IsTransient ? MessageStatus.Failed : MessageStatus.Rejected, utcNow);
                Application.Observability.CampaignMetrics.MessagesFailed.Add(1, channelTag);
            }

            _db.DeliveryEvents.Add(new DeliveryEvent
            {
                MessageId = message.Id,
                Status = message.Status,
                Detail = message.LastError,
                OccurredAtUtc = utcNow
            });

            if (++processedSinceCheck >= SaveChunkSize)
            {
                processedSinceCheck = 0;
                await _db.SaveChangesAsync();

                // Cheap cancellation probe between chunks.
                var currentStatus = await _db.Campaigns.AsNoTracking()
                    .Where(c => c.Id == campaignId)
                    .Select(c => c.Status)
                    .FirstAsync();
                if (currentStatus == CampaignStatus.Cancelled)
                {
                    await ExpireQueuedInRange(campaignId, message.Id + 1, lastMessageId);
                    return;
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task FinalizeAsync(Guid organizationId, Guid campaignId)
    {
        _tenantSetter.Set(organizationId);
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null || campaign.IsTerminal)
        {
            return;
        }

        var statusCounts = await _db.Messages
            .Where(m => m.CampaignId == campaignId)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int Count(params MessageStatus[] statuses) =>
            statusCounts.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

        var outstanding = Count(MessageStatus.Queued, MessageStatus.Processing);
        if (outstanding > 0)
        {
            _jobClient.Schedule<ICampaignProcessingJob>(
                j => j.FinalizeAsync(organizationId, campaignId), FinalizerDelay);
            return;
        }

        var total = statusCounts.Sum(s => s.Count);
        var failed = Count(MessageStatus.Failed, MessageStatus.Rejected, MessageStatus.Expired);
        campaign.SentCount = Count(MessageStatus.Sent, MessageStatus.Delivered, MessageStatus.Read);
        campaign.DeliveredCount = Count(MessageStatus.Delivered, MessageStatus.Read);
        campaign.FailedCount = failed;
        campaign.TransitionTo(CampaignStateMachine.Finalize(total, failed));
        campaign.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Application.Observability.CampaignMetrics.CampaignsCompleted.Add(1,
            new KeyValuePair<string, object?>("status", campaign.Status.ToString()));

        if (!string.IsNullOrEmpty(campaign.CallbackUrl))
        {
            _jobClient.Enqueue<ICampaignProcessingJob>(
                j => j.NotifyCompletionAsync(organizationId, campaignId));
        }

        await RaiseCompletionNotificationAsync(organizationId, campaign, total, failed);

        _logger.LogInformation("Finalized campaign {CampaignId} as {Status} (sent {Sent}, failed {Failed})",
            campaignId, campaign.Status, campaign.SentCount, campaign.FailedCount);
    }

    private async Task RaiseCompletionNotificationAsync(Guid organizationId, Campaign campaign, int total, int failed)
    {
        var failureRatio = total == 0 ? 0 : (double)failed / total;
        var settings = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);
        var threshold = settings?.HighFailureRateThreshold ?? 0.25;

        if (campaign.Status == CampaignStatus.Failed)
        {
            await _notifications.NotifyAsync(organizationId, Application.Notifications.NotificationEventTypes.CampaignFailed,
                $"Campaign failed: {campaign.Name}",
                $"Campaign '{campaign.Name}' ({campaign.TrackingId}) failed: all {total} messages were unsuccessful.",
                default);
        }
        else if (failureRatio >= threshold && failed > 0)
        {
            await _notifications.NotifyAsync(organizationId, Application.Notifications.NotificationEventTypes.HighFailureRate,
                $"High failure rate: {campaign.Name}",
                $"Campaign '{campaign.Name}' ({campaign.TrackingId}) finished with a " +
                $"{failureRatio:P0} failure rate ({failed}/{total}), at or above the {threshold:P0} threshold.",
                default);
        }
        else if (campaign.Status is CampaignStatus.Completed or CampaignStatus.CompletedWithErrors)
        {
            await _notifications.NotifyAsync(organizationId, Application.Notifications.NotificationEventTypes.CampaignCompleted,
                $"Campaign completed: {campaign.Name}",
                $"Campaign '{campaign.Name}' ({campaign.TrackingId}) completed: " +
                $"{total - failed}/{total} sent successfully.",
                default);
        }
    }

    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 7200])]
    public async Task NotifyCompletionAsync(Guid organizationId, Guid campaignId)
    {
        _tenantSetter.Set(organizationId);
        var campaign = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null || string.IsNullOrEmpty(campaign.CallbackUrl) || !campaign.IsTerminal)
        {
            return;
        }

        if (!await Security.SsrfGuard.IsSafePublicUrlAsync(campaign.CallbackUrl, CancellationToken.None))
        {
            _logger.LogWarning(
                "Campaign {CampaignId} callback url rejected by SSRF guard: {CallbackUrl}",
                campaignId, campaign.CallbackUrl);
            return; // permanent: do not retry a forbidden destination
        }

        var client = _httpClientFactory.CreateClient("campaign-callbacks");
        using var response = await client.PostAsJsonAsync(campaign.CallbackUrl, new
        {
            campaignId = campaign.Id,
            trackingId = campaign.TrackingId,
            status = campaign.Status.ToString(),
            totalRecipients = campaign.TotalRecipients,
            sent = campaign.SentCount,
            delivered = campaign.DeliveredCount,
            failed = campaign.FailedCount,
            completedAtUtc = campaign.CompletedAtUtc
        });
        response.EnsureSuccessStatusCode(); // non-2xx throws → Hangfire retries with backoff

        _logger.LogInformation("Delivered completion callback for campaign {CampaignId} to {CallbackUrl}",
            campaignId, campaign.CallbackUrl);
    }

    private async Task ExpireQueuedInRange(Guid campaignId, long firstMessageId, long lastMessageId)
    {
        if (firstMessageId > lastMessageId) return;
        await _db.Messages
            .Where(m => m.CampaignId == campaignId
                        && m.Id >= firstMessageId && m.Id <= lastMessageId
                        && m.Status == MessageStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, MessageStatus.Expired)
                .SetProperty(m => m.UpdatedAtUtc, DateTime.UtcNow));
    }

    private static IReadOnlyDictionary<string, string>? Deserialize(string? json) =>
        string.IsNullOrEmpty(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
