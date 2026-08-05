using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using CampaignManager.Domain.StateMachine;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.StateMachine;

public class CampaignStateMachineTests
{
    [Theory]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Queued)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Scheduled)]
    [InlineData(CampaignStatus.Scheduled, CampaignStatus.Queued)]
    [InlineData(CampaignStatus.Scheduled, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Queued, CampaignStatus.Processing)]
    [InlineData(CampaignStatus.Queued, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Processing, CampaignStatus.Completed)]
    [InlineData(CampaignStatus.Processing, CampaignStatus.CompletedWithErrors)]
    [InlineData(CampaignStatus.Processing, CampaignStatus.Failed)]
    [InlineData(CampaignStatus.Processing, CampaignStatus.Cancelled)]
    public void Allows_valid_transitions(CampaignStatus from, CampaignStatus to) =>
        CampaignStateMachine.CanTransition(from, to).Should().BeTrue();

    [Theory]
    [InlineData(CampaignStatus.Completed, CampaignStatus.Processing)]
    [InlineData(CampaignStatus.Cancelled, CampaignStatus.Queued)]
    [InlineData(CampaignStatus.Failed, CampaignStatus.Queued)]
    [InlineData(CampaignStatus.Processing, CampaignStatus.Queued)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Processing)]
    public void Rejects_invalid_transitions(CampaignStatus from, CampaignStatus to) =>
        CampaignStateMachine.CanTransition(from, to).Should().BeFalse();

    [Theory]
    [InlineData(100, 0, CampaignStatus.Completed)]
    [InlineData(100, 5, CampaignStatus.CompletedWithErrors)]
    [InlineData(100, 100, CampaignStatus.Failed)]
    public void Finalize_maps_failure_counts(int total, int failed, CampaignStatus expected) =>
        CampaignStateMachine.Finalize(total, failed).Should().Be(expected);

    [Fact]
    public void Entity_transition_throws_on_invalid_move()
    {
        var campaign = new Campaign { TrackingId = "CMP-X", Name = "t", Sender = "s" };
        var act = () => campaign.TransitionTo(CampaignStatus.Processing);
        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Terminal_statuses_are_terminal()
    {
        CampaignStateMachine.IsTerminal(CampaignStatus.Completed).Should().BeTrue();
        CampaignStateMachine.IsTerminal(CampaignStatus.Cancelled).Should().BeTrue();
        CampaignStateMachine.IsTerminal(CampaignStatus.Processing).Should().BeFalse();
    }
}
