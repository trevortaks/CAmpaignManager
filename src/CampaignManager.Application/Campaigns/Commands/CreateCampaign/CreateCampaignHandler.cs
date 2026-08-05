using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Application.Campaigns.Commands.CreateCampaign;

public sealed class CreateCampaignHandler : IRequestHandler<CreateCampaignCommand, CreateCampaignResponse>
{
    private const int InsertChunkSize = 1_000;

    /// <summary>Above this recipient count, use SqlBulkCopy for recipients and defer message
    /// creation to the dispatch job (lazy creation), avoiding a doubled EF write here.
    /// Tuned low for demonstrability; production deployments may prefer 10,000+.</summary>
    public const int BulkCopyThreshold = 2_000;

    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ICampaignDispatcher _dispatcher;
    private readonly IBulkRecipientWriter _bulkWriter;
    private readonly ILogger<CreateCampaignHandler> _logger;

    public CreateCampaignHandler(
        IAppDbContext db,
        ICurrentTenant tenant,
        ICampaignDispatcher dispatcher,
        IBulkRecipientWriter bulkWriter,
        ILogger<CreateCampaignHandler> logger)
    {
        _db = db;
        _tenant = tenant;
        _dispatcher = dispatcher;
        _bulkWriter = bulkWriter;
        _logger = logger;
    }

    public async Task<CreateCampaignResponse> Handle(CreateCampaignCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId
            ?? throw new DomainException("No organization context for the current caller.");
        var request = command.Request;
        var channel = Enum.Parse<Channel>(request.Channel, ignoreCase: true);
        var utcNow = DateTime.UtcNow;

        if (request.TemplateId.HasValue)
        {
            var templateExists = await _db.MessageTemplates
                .AnyAsync(t => t.Id == request.TemplateId.Value && t.Channel == channel && t.IsActive, ct);
            if (!templateExists)
            {
                throw new NotFoundException(nameof(MessageTemplate), request.TemplateId.Value);
            }
        }

        // Deduplicate recipients by address (case-insensitive), keeping the first occurrence.
        var uniqueRecipients = request.Recipients
            .GroupBy(r => r.Address.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SeriesId = command.SeriesId,
            TrackingId = Campaign.NewTrackingId(),
            Name = request.Name,
            Channel = channel,
            Sender = request.Sender,
            Subject = request.Subject,
            MessageBody = request.MessageBody,
            TemplateId = request.TemplateId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            Priority = request.Priority,
            TagsJson = request.Tags is { Count: > 0 } ? JsonSerializer.Serialize(request.Tags) : null,
            MetadataJson = request.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(request.Metadata) : null,
            CallbackUrl = request.CallbackUrl,
            TotalRecipients = uniqueRecipients.Count,
            CreatedByUserId = _tenant.UserId,
            CreatedAtUtc = utcNow
        };
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);

        if (uniqueRecipients.Count > BulkCopyThreshold)
        {
            // SqlBulkCopy path: recipients only. Messages are created lazily by the dispatch
            // job (CampaignProcessingJob.DispatchAsync), halving the write volume here for
            // very large campaigns — see docs/11-scalability.md.
            await _bulkWriter.BulkInsertRecipientsAsync(organizationId, campaign.Id, uniqueRecipients, ct);
            _logger.LogInformation(
                "Campaign {CampaignId}: bulk-inserted {Count} recipients (over threshold {Threshold}); " +
                "messages will be created lazily at dispatch", campaign.Id, uniqueRecipients.Count, BulkCopyThreshold);
        }
        else
        {
            foreach (var chunk in uniqueRecipients.Chunk(InsertChunkSize))
            {
                var recipients = chunk.Select(dto => new CampaignRecipient
                {
                    CampaignId = campaign.Id,
                    OrganizationId = organizationId,
                    Address = dto.Address.Trim(),
                    PersonalizationJson = dto.Personalization is { Count: > 0 }
                        ? JsonSerializer.Serialize(dto.Personalization)
                        : null,
                    CreatedAtUtc = utcNow
                }).ToList();
                _db.CampaignRecipients.AddRange(recipients);
                await _db.SaveChangesAsync(ct);

                _db.Messages.AddRange(recipients.Select(r => new Message
                {
                    PublicId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    CampaignId = campaign.Id,
                    RecipientId = r.Id,
                    Channel = channel,
                    QueuedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                }));
                await _db.SaveChangesAsync(ct);
            }
        }

        if (request.ScheduledAtUtc is { } scheduledAt && scheduledAt > DateTime.UtcNow)
        {
            campaign.TransitionTo(CampaignStatus.Scheduled);
            campaign.ScheduledJobId = _dispatcher.ScheduleDispatch(organizationId, campaign.Id, scheduledAt);
        }
        else
        {
            campaign.TransitionTo(CampaignStatus.Queued);
            _dispatcher.EnqueueDispatch(organizationId, campaign.Id);
        }

        await _db.SaveChangesAsync(ct);

        Observability.CampaignMetrics.CampaignsCreated.Add(1,
            new KeyValuePair<string, object?>("channel", channel.ToString()));

        _logger.LogInformation(
            "Created campaign {CampaignId} ({TrackingId}) with {RecipientCount} recipients, status {Status}",
            campaign.Id, campaign.TrackingId, campaign.TotalRecipients, campaign.Status);

        return new CreateCampaignResponse(campaign.Id, campaign.TrackingId, campaign.Status.ToString());
    }
}
