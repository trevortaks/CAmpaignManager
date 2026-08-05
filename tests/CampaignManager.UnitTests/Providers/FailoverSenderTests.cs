using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CampaignManager.UnitTests.Providers;

public class FailoverSenderTests
{
    private static readonly ProviderSendRequest Request = new(
        Guid.NewGuid(), "+263771234567", "hello", null, "TEST",
        new Dictionary<string, string>());

    private static readonly ProviderCredentials EmptyCredentials = new(
        new Dictionary<string, string>(), new Dictionary<string, string>());

    private readonly FailoverSender _sender = new(NullLogger<FailoverSender>.Instance);

    private static ResolvedProvider Provider(SendResult result, out IChannelProvider mock)
    {
        mock = Substitute.For<IChannelProvider>();
        mock.Channel.Returns(Channel.Sms);
        mock.ProviderKey.Returns("mock");
        mock.SendAsync(Arg.Any<ProviderSendRequest>(), Arg.Any<ProviderCredentials>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return new ResolvedProvider(Guid.NewGuid(), mock, EmptyCredentials);
    }

    [Fact]
    public async Task Uses_first_provider_when_it_succeeds()
    {
        var first = Provider(SendResult.Ok("pm-1"), out var firstMock);
        var second = Provider(SendResult.Ok("pm-2"), out var secondMock);

        var (result, configId) = await _sender.SendAsync([first, second], Request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProviderMessageId.Should().Be("pm-1");
        configId.Should().Be(first.ProviderConfigurationId);
        await secondMock.DidNotReceive().SendAsync(
            Arg.Any<ProviderSendRequest>(), Arg.Any<ProviderCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_over_to_next_provider_on_transient_failure()
    {
        var first = Provider(SendResult.TransientFailure("503", "unavailable"), out _);
        var second = Provider(SendResult.Ok("pm-2"), out _);

        var (result, configId) = await _sender.SendAsync([first, second], Request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProviderMessageId.Should().Be("pm-2");
        configId.Should().Be(second.ProviderConfigurationId);
    }

    [Fact]
    public async Task Rejection_short_circuits_without_failover()
    {
        var first = Provider(SendResult.Rejected("invalid_number", "bad address"), out _);
        var second = Provider(SendResult.Ok("pm-2"), out var secondMock);

        var (result, _) = await _sender.SendAsync([first, second], Request, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_number");
        await secondMock.DidNotReceive().SendAsync(
            Arg.Any<ProviderSendRequest>(), Arg.Any<ProviderCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provider_exception_is_treated_as_transient_and_fails_over()
    {
        var throwingMock = Substitute.For<IChannelProvider>();
        throwingMock.ProviderKey.Returns("boom");
        throwingMock.SendAsync(Arg.Any<ProviderSendRequest>(), Arg.Any<ProviderCredentials>(), Arg.Any<CancellationToken>())
            .Returns<Task<SendResult>>(_ => throw new HttpRequestException("connection refused"));
        var first = new ResolvedProvider(Guid.NewGuid(), throwingMock, EmptyCredentials);
        var second = Provider(SendResult.Ok("pm-2"), out _);

        var (result, _) = await _sender.SendAsync([first, second], Request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProviderMessageId.Should().Be("pm-2");
    }

    [Fact]
    public async Task All_transient_failures_returns_last_failure()
    {
        var first = Provider(SendResult.TransientFailure("503", "a"), out _);
        var second = Provider(SendResult.TransientFailure("504", "b"), out _);

        var (result, configId) = await _sender.SendAsync([first, second], Request, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("504");
        configId.Should().Be(second.ProviderConfigurationId);
    }

    [Fact]
    public async Task Empty_provider_list_returns_no_provider_failure()
    {
        var (result, configId) = await _sender.SendAsync([], Request, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("no_provider");
        configId.Should().BeNull();
    }
}
