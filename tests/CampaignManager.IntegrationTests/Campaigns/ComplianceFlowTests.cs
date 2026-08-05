using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampaignManager.Contracts.Auth;
using FluentAssertions;
using Xunit;

namespace CampaignManager.IntegrationTests.Campaigns;

[Collection("ApiFactory")]
public sealed class ComplianceFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ComplianceFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var tokenResponse = await client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest { Email = "admin@demo.local", Password = "Admin!Passw0rd1" });
        var token = (await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    [Fact]
    public async Task Suppressing_an_address_twice_does_not_duplicate_it()
    {
        var client = await AuthedClientAsync();
        var address = $"opt-out-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/api/compliance/suppressions", new { Address = address, Reason = "unsubscribe" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync("/api/compliance/suppressions", new { Address = address, Reason = "unsubscribe" });
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<SuppressionDto>>("/api/compliance/suppressions");
        list!.Count(s => s.Address == address).Should().Be(1, "the same address must not produce two suppression rows");
    }

    [Fact]
    public async Task Erase_redacts_matching_recipient_rows()
    {
        var client = await AuthedClientAsync();
        var address = $"+2637710{Random.Shared.Next(10000, 99999)}";

        await client.PostAsJsonAsync("/api/campaigns", new
        {
            Name = "Erasure test",
            Channel = "Sms",
            Sender = "ITEST",
            MessageBody = "hi",
            Recipients = new[] { new { Address = address } }
        });

        var eraseResponse = await client.PostAsJsonAsync("/api/compliance/erase", new { Address = address });
        eraseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await eraseResponse.Content.ReadFromJsonAsync<EraseResultDto>();
        result!.RowsRedacted.Should().BeGreaterThan(0);
    }

    private sealed record SuppressionDto(Guid Id, string Address, string? Channel, string Reason, DateTime CreatedAtUtc);
    private sealed record EraseResultDto(int RowsRedacted);
}
