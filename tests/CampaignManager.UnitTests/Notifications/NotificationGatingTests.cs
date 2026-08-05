using CampaignManager.Application.Notifications;
using CampaignManager.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.Notifications;

public class NotificationGatingTests
{
    [Theory]
    [InlineData(NotificationEventTypes.ProviderOffline)]
    [InlineData(NotificationEventTypes.ProviderAuthFailed)]
    [InlineData(NotificationEventTypes.CampaignFailed)]
    [InlineData(NotificationEventTypes.HighFailureRate)]
    public void Defaults_enable_failure_related_events_when_unconfigured(string eventType) =>
        NotificationService.IsEnabled(null, eventType).Should().BeTrue();

    [Fact]
    public void Defaults_do_not_enable_campaign_completed_when_unconfigured() =>
        NotificationService.IsEnabled(null, NotificationEventTypes.CampaignCompleted).Should().BeFalse();

    [Fact]
    public void Respects_explicit_settings_toggles()
    {
        var settings = new NotificationSettings
        {
            NotifyProviderOffline = false,
            NotifyCampaignCompleted = true,
            NotifyCampaignFailed = false
        };

        NotificationService.IsEnabled(settings, NotificationEventTypes.ProviderOffline).Should().BeFalse();
        NotificationService.IsEnabled(settings, NotificationEventTypes.CampaignCompleted).Should().BeTrue();
        NotificationService.IsEnabled(settings, NotificationEventTypes.CampaignFailed).Should().BeFalse();
        NotificationService.IsEnabled(settings, NotificationEventTypes.HighFailureRate).Should().BeFalse();
    }

    [Fact]
    public void Unknown_event_type_is_never_enabled() =>
        NotificationService.IsEnabled(new NotificationSettings(), "SomethingElse").Should().BeFalse();
}
