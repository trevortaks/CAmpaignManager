using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Models.Auth;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Credentials;

public sealed class JwtCredentialTests
{
    [Fact]
    public async Task ApplyAsync_sets_bearer_header_from_provider()
    {
        var credential = new JwtCredential(_ => Task.FromResult(new TokenResponse("tok-1", DateTime.UtcNow.AddMinutes(30))));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/api/campaigns");

        await credential.ApplyAsync(request, CancellationToken.None);

        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("tok-1");
    }

    [Fact]
    public async Task Token_is_cached_across_calls_until_near_expiry()
    {
        var fetchCount = 0;
        var credential = new JwtCredential(_ =>
        {
            fetchCount++;
            return Task.FromResult(new TokenResponse($"tok-{fetchCount}", DateTime.UtcNow.AddMinutes(30)));
        });

        using var r1 = new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/a");
        using var r2 = new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/b");
        await credential.ApplyAsync(r1, CancellationToken.None);
        await credential.ApplyAsync(r2, CancellationToken.None);

        fetchCount.Should().Be(1);
        r2.Headers.Authorization!.Parameter.Should().Be("tok-1");
    }

    [Fact]
    public async Task Token_is_refreshed_once_it_is_within_the_expiry_skew()
    {
        var fetchCount = 0;
        var credential = new JwtCredential(_ =>
        {
            fetchCount++;
            // Already inside the 60s safety skew on the very first fetch.
            return Task.FromResult(new TokenResponse($"tok-{fetchCount}", DateTime.UtcNow.AddSeconds(10)));
        });

        using var r1 = new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/a");
        using var r2 = new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/b");
        await credential.ApplyAsync(r1, CancellationToken.None);
        await credential.ApplyAsync(r2, CancellationToken.None);

        fetchCount.Should().Be(2, "the cached token was already within the refresh skew");
    }

    [Fact]
    public async Task Concurrent_calls_trigger_only_one_fetch()
    {
        var fetchCount = 0;
        var credential = new JwtCredential(async _ =>
        {
            Interlocked.Increment(ref fetchCount);
            await Task.Delay(50);
            return new TokenResponse("tok", DateTime.UtcNow.AddMinutes(30));
        });

        var requests = Enumerable.Range(0, 10)
            .Select(_ => new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/a"))
            .ToList();

        await Task.WhenAll(requests.Select(r => credential.ApplyAsync(r, CancellationToken.None).AsTask()));

        fetchCount.Should().Be(1);
        foreach (var request in requests) request.Dispose();
    }
}
