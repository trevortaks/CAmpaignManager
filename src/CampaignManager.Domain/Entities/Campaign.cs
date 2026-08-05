using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using CampaignManager.Domain.StateMachine;

namespace CampaignManager.Domain.Entities;

public class Campaign
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>Set when this campaign was materialized from a CampaignSeries occurrence.</summary>
    public Guid? SeriesId { get; set; }
    public required string TrackingId { get; set; }
    public required string Name { get; set; }
    public Channel Channel { get; set; }
    public CampaignStatus Status { get; private set; } = CampaignStatus.Draft;
    public Guid? TemplateId { get; set; }
    public string? MessageBody { get; set; }
    public string? Subject { get; set; }
    public required string Sender { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public string? ScheduledJobId { get; set; }
    public int Priority { get; set; }
    public string? TagsJson { get; set; }
    public string? MetadataJson { get; set; }
    public string? CallbackUrl { get; set; }

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Organization? Organization { get; set; }
    public MessageTemplate? Template { get; set; }

    public bool IsTerminal => CampaignStateMachine.IsTerminal(Status);

    public void TransitionTo(CampaignStatus target)
    {
        if (!CampaignStateMachine.CanTransition(Status, target))
        {
            throw new InvalidStateTransitionException(nameof(Campaign), Status.ToString(), target.ToString());
        }

        Status = target;
    }

    public static string NewTrackingId() =>
        $"CMP-{Convert.ToHexString(Guid.NewGuid().ToByteArray())[..16]}";
}
