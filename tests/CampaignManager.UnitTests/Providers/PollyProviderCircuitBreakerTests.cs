using CampaignManager.Application.Providers;
using CampaignManager.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CampaignManager.UnitTests.Providers;

public class PollyProviderCircuitBreakerTests
{
    private static Task<SendResult> Transient(CancellationToken _) =>
        Task.FromResult(SendResult.TransientFailure("boom", "simulated"));

    private static Task<SendResult> Success(CancellationToken _) =>
        Task.FromResult(SendResult.Ok("ok"));

    [Fact]
    public async Task Opens_after_repeated_transient_failures_and_fails_fast()
    {
        var breaker = new PollyProviderCircuitBreaker(NullLogger<PollyProviderCircuitBreaker>.Instance);
        var providerId = Guid.NewGuid();

        // MinimumThroughput=5: drive enough transient failures to trip the breaker.
        for (var i = 0; i < 5; i++)
        {
            var result = await breaker.ExecuteAsync(providerId, Transient, CancellationToken.None);
            result.Success.Should().BeFalse();
        }

        var afterOpen = await breaker.ExecuteAsync(providerId, Success, CancellationToken.None);

        afterOpen.Success.Should().BeFalse();
        afterOpen.ErrorCode.Should().Be("circuit_open");
    }

    [Fact]
    public async Task Different_providers_have_independent_breakers()
    {
        var breaker = new PollyProviderCircuitBreaker(NullLogger<PollyProviderCircuitBreaker>.Instance);
        var failingProvider = Guid.NewGuid();
        var healthyProvider = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            await breaker.ExecuteAsync(failingProvider, Transient, CancellationToken.None);
        }

        var healthyResult = await breaker.ExecuteAsync(healthyProvider, Success, CancellationToken.None);
        healthyResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Successful_calls_never_open_the_circuit()
    {
        var breaker = new PollyProviderCircuitBreaker(NullLogger<PollyProviderCircuitBreaker>.Instance);
        var providerId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            var result = await breaker.ExecuteAsync(providerId, Success, CancellationToken.None);
            result.Success.Should().BeTrue();
        }
    }
}
