using System.Net;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Clients;

public sealed class AuthClientTests
{
    [Fact]
    public async Task GetTokenAsync_posts_email_and_password_and_returns_token()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK,
            """{"accessToken":"jwt-abc","expiresAtUtc":"2026-01-01T00:00:00Z"}"""));
        var client = TestClientFactory.Create(stub);

        var token = await client.Auth.GetTokenAsync("admin@demo.local", "secret");

        token.AccessToken.Should().Be("jwt-abc");
        stub.RequestBodies[0].Should().Contain("admin@demo.local").And.Contain("secret");
        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/auth/token");
    }
}
