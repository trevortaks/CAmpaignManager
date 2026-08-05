using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.StateMachine;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.StateMachine;

public class MessageStateMachineTests
{
    [Theory]
    [InlineData(MessageStatus.Queued, MessageStatus.Processing)]
    [InlineData(MessageStatus.Queued, MessageStatus.Expired)]
    [InlineData(MessageStatus.Processing, MessageStatus.Sent)]
    [InlineData(MessageStatus.Processing, MessageStatus.Failed)]
    [InlineData(MessageStatus.Processing, MessageStatus.Rejected)]
    [InlineData(MessageStatus.Sent, MessageStatus.Delivered)]
    [InlineData(MessageStatus.Sent, MessageStatus.Read)]
    [InlineData(MessageStatus.Delivered, MessageStatus.Read)]
    public void Allows_valid_transitions(MessageStatus from, MessageStatus to) =>
        MessageStateMachine.CanTransition(from, to).Should().BeTrue();

    [Theory]
    [InlineData(MessageStatus.Read, MessageStatus.Delivered)]
    [InlineData(MessageStatus.Failed, MessageStatus.Sent)]
    [InlineData(MessageStatus.Delivered, MessageStatus.Sent)]
    [InlineData(MessageStatus.Queued, MessageStatus.Delivered)]
    public void Rejects_invalid_transitions(MessageStatus from, MessageStatus to) =>
        MessageStateMachine.CanTransition(from, to).Should().BeFalse();

    [Fact]
    public void Webhook_status_is_monotonic()
    {
        // late "Sent" after Delivered must be ignored
        MessageStateMachine.ShouldApplyWebhookStatus(MessageStatus.Delivered, MessageStatus.Sent)
            .Should().BeFalse();
        MessageStateMachine.ShouldApplyWebhookStatus(MessageStatus.Sent, MessageStatus.Delivered)
            .Should().BeTrue();
        MessageStateMachine.ShouldApplyWebhookStatus(MessageStatus.Delivered, MessageStatus.Read)
            .Should().BeTrue();
    }

    [Fact]
    public void Webhook_failed_applies_from_non_terminal_only()
    {
        MessageStateMachine.ShouldApplyWebhookStatus(MessageStatus.Sent, MessageStatus.Failed)
            .Should().BeTrue();
        MessageStateMachine.ShouldApplyWebhookStatus(MessageStatus.Read, MessageStatus.Failed)
            .Should().BeFalse();
    }

    [Fact]
    public void Entity_applies_webhook_and_stamps_delivered_time()
    {
        var utcNow = DateTime.UtcNow;
        var message = new Message();
        message.TransitionTo(MessageStatus.Processing, utcNow);
        message.TransitionTo(MessageStatus.Sent, utcNow);

        message.TryApplyWebhookStatus(MessageStatus.Delivered, utcNow).Should().BeTrue();
        message.Status.Should().Be(MessageStatus.Delivered);
        message.DeliveredAtUtc.Should().Be(utcNow);

        message.TryApplyWebhookStatus(MessageStatus.Sent, utcNow).Should().BeFalse();
        message.Status.Should().Be(MessageStatus.Delivered);
    }
}
