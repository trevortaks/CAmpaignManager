using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://campaigns.test/")]
    public void Base_address_must_be_absolute_http_or_https(string address)
    {
        var options = ValidOptions();
        options.BaseAddress = new Uri(address, UriKind.RelativeOrAbsolute);

        FluentActions.Invoking(options.Validate).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Timeout_must_be_positive()
    {
        var options = ValidOptions();
        options.Timeout = TimeSpan.Zero;

        FluentActions.Invoking(options.Validate).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Retry_count_must_be_in_sensible_range(int retries)
    {
        var options = ValidOptions();
        options.MaxRetryAttempts = retries;

        FluentActions.Invoking(options.Validate).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_paging_is_rejected_before_sending(int page, int pageSize)
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("must not send"));
        using var client = TestClientFactory.Create(handler);

        await FluentActions.Awaiting(() => client.Campaigns.SearchAsync(
                new CampaignSearchOptions { Page = page, PageSize = pageSize }))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        handler.Requests.Should().BeEmpty();
    }

    private static CampaignManagerClientOptions ValidOptions() => new()
    {
        BaseAddress = new Uri("https://campaigns.test/"),
        Credential = new ApiKeyCredential("cmk_test")
    };
}
