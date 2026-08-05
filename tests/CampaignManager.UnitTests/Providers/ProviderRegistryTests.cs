using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using CampaignManager.Infrastructure.Providers.Fake;
using CampaignManager.Infrastructure.Providers.Registry;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CampaignManager.UnitTests.Providers;

public class ProviderRegistryTests
{
    private static ProviderRegistry NewRegistry() => new(
    [
        new FakeSmsProvider(NullLogger<FakeSmsProvider>.Instance),
        new FakeEmailProvider(NullLogger<FakeEmailProvider>.Instance),
        new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance)
    ]);

    [Fact]
    public void Resolves_registered_provider_by_channel_and_key()
    {
        var provider = NewRegistry().Resolve(Channel.Sms, "fake-sms");
        provider.Should().BeOfType<FakeSmsProvider>();
    }

    [Fact]
    public void Same_key_different_channel_does_not_match()
    {
        NewRegistry().TryResolve(Channel.Email, "fake-sms", out _).Should().BeFalse();
    }

    [Fact]
    public void Unknown_key_throws_on_resolve()
    {
        var act = () => NewRegistry().Resolve(Channel.Sms, "nonexistent");
        act.Should().Throw<InvalidOperationException>().WithMessage("*nonexistent*");
    }
}
