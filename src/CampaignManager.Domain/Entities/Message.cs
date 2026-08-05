using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using CampaignManager.Domain.StateMachine;

namespace CampaignManager.Domain.Entities;

public class Message
{
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CampaignId { get; set; }
    public long RecipientId { get; set; }
    public Channel Channel { get; set; }
    public MessageStatus Status { get; private set; } = MessageStatus.Queued;
    public Guid? ProviderConfigurationId { get; set; }
    public string? ProviderMessageId { get; set; }
    public byte AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime QueuedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Campaign? Campaign { get; set; }
    public CampaignRecipient? Recipient { get; set; }

    public bool IsTerminal => MessageStateMachine.IsTerminal(Status);

    public void TransitionTo(MessageStatus target, DateTime utcNow)
    {
        if (!MessageStateMachine.CanTransition(Status, target))
        {
            throw new InvalidStateTransitionException(nameof(Message), Status.ToString(), target.ToString());
        }

        Status = target;
        UpdatedAtUtc = utcNow;
        if (target == MessageStatus.Sent) SentAtUtc = utcNow;
        if (target == MessageStatus.Delivered) DeliveredAtUtc = utcNow;
    }

    /// <summary>Applies a provider-reported status if it advances the message (monotonic).</summary>
    public bool TryApplyWebhookStatus(MessageStatus reported, DateTime utcNow)
    {
        if (!MessageStateMachine.ShouldApplyWebhookStatus(Status, reported)) return false;
        Status = reported;
        UpdatedAtUtc = utcNow;
        if (reported == MessageStatus.Delivered) DeliveredAtUtc = utcNow;
        return true;
    }
}
