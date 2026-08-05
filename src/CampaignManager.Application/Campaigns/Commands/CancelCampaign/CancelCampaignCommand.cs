using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Application.Campaigns.Commands.CancelCampaign;

public sealed record CancelCampaignCommand(Guid CampaignId) : IRequest;

public sealed class CancelCampaignHandler : IRequestHandler<CancelCampaignCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICampaignDispatcher _dispatcher;
    private readonly ILogger<CancelCampaignHandler> _logger;

    public CancelCampaignHandler(
        IAppDbContext db, ICampaignDispatcher dispatcher, ILogger<CancelCampaignHandler> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Handle(CancelCampaignCommand command, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == command.CampaignId, ct)
            ?? throw new NotFoundException(nameof(Campaign), command.CampaignId);

        // Throws InvalidStateTransitionException (409) if already terminal.
        campaign.TransitionTo(CampaignStatus.Cancelled);
        campaign.CompletedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(campaign.ScheduledJobId))
        {
            _dispatcher.DeleteScheduledJob(campaign.ScheduledJobId);
            campaign.ScheduledJobId = null;
        }

        await _db.SaveChangesAsync(ct);

        // Expire everything still queued; in-flight batches notice the Cancelled
        // status at their periodic check and stop.
        var expired = await _db.Messages
            .Where(m => m.CampaignId == campaign.Id && m.Status == MessageStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, MessageStatus.Expired)
                .SetProperty(m => m.UpdatedAtUtc, DateTime.UtcNow), ct);

        _logger.LogInformation("Cancelled campaign {CampaignId}; expired {Count} queued messages",
            campaign.Id, expired);
    }
}
