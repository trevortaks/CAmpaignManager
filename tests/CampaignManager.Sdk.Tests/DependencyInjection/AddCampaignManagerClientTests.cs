using CampaignManager.Sdk;
using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.Sdk.Tests.DependencyInjection;

public sealed class AddCampaignManagerClientTests
{
    [Fact]
    public void Registers_a_resolvable_CampaignManagerClient_with_the_configured_base_address()
    {
        var services = new ServiceCollection();

        services.AddCampaignManagerClient(options =>
        {
            options.BaseAddress = new Uri("https://campaigns.test/");
            options.Credential = new ApiKeyCredential("cmk_test");
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<CampaignManagerClient>();

        client.Should().NotBeNull();
        client.Campaigns.Should().NotBeNull();
    }

    [Fact]
    public void Throws_when_BaseAddress_is_not_configured()
    {
        var services = new ServiceCollection();

        var act = () => services.AddCampaignManagerClient(options =>
        {
            options.Credential = new ApiKeyCredential("cmk_test");
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*BaseAddress*");
    }

    [Fact]
    public void Throws_when_Credential_is_not_configured()
    {
        var services = new ServiceCollection();

        var act = () => services.AddCampaignManagerClient(options =>
        {
            options.BaseAddress = new Uri("https://campaigns.test/");
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Credential*");
    }
}
