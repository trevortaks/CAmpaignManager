using CampaignManager.Sdk.Auth;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Credentials;

public sealed class ApiKeyCredentialTests
{
    [Fact]
    public async Task ApplyAsync_sets_X_Api_Key_header()
    {
        var credential = new ApiKeyCredential("cmk_abc123");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://campaigns.test/api/campaigns");

        await credential.ApplyAsync(request, CancellationToken.None);

        request.Headers.GetValues("X-Api-Key").Should().ContainSingle().Which.Should().Be("cmk_abc123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_keys(string? apiKey)
    {
        var act = () => new ApiKeyCredential(apiKey!);
        act.Should().Throw<ArgumentException>();
    }
}
