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
    private readonly ILogger<CampaignProcessingJob> _logger;

    public CampaignProcessingJob(
        IAppDbContext db,
        ITenantSetter tenantSetter,
        IProviderSelector providerSelector,
        FailoverSender failoverSender,
        ITemplateRenderer templateRenderer,
        IBackgroundJobClient jobClient,
        ILogger<CampaignProcessingJob> logger)
    {
        _db = db;
        _tenantSetter = tenantSetter;
        _providerSelector = providerSelector;
        _failoverSender = failoverSender;
        _templateRenderer = templateRenderer;
        _jobClient = jobClient;
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

        var processedSinceCheck = 0;
        foreach (var message in messages)
        {
            var personalization = Deserialize(message.Recipient?.PersonalizationJson);
            var body = _templateRenderer.Render(bodyTemplate, personalization);
            var subject = subjectTemplate is null ? null : _templateRenderer.Render(subjectTemplate, personalization);
            var utcNow = DateTime.UtcNow;

            message.TransitionTo(MessageStatus.Processing, utcNow);
            message.AttemptCount++;

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

            message.ProviderConfigurationId = providerConfigId;
            utcNow = DateTime.UtcNow;
            if (result.Success)
            {
                message.ProviderMessageId = result.ProviderMessageId;
                message.TransitionTo(MessageStatus.Sent, utcNow);
            }
            else
            {
                message.LastError = Truncate($"{result.ErrorCode}: {result.ErrorMessage}", 1024);
                message.TransitionTo(
                    result.IsTransient ? MessageStatus.Failed : MessageStatus.Rejected, utcNow);
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

        _logger.LogInformation("Finalized campaign {CampaignId} as {Status} (sent {Sent}, failed {Failed})",
            campaignId, campaign.Status, campaign.SentCount, campaign.FailedCount);
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
