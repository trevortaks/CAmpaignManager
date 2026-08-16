using System.Net;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Clients;

public sealed class ComplianceClientTests
{
    [Fact]
    public async Task SuppressAsync_unwraps_suppressionId_envelope()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.Created,
            """{"suppressionId":"11111111-1111-1111-1111-111111111111"}"""));
        var client = TestClientFactory.Create(stub);

        var id = await client.Compliance.SuppressAsync("someone@example.com", reason: "unsubscribe");

        id.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    [Fact]
    public async Task EraseAsync_unwraps_rowsRedacted_envelope()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK,
            """{"rowsRedacted":3}"""));
        var client = TestClientFactory.Create(stub);

        var rows = await client.Compliance.EraseAsync("someone@example.com");

        rows.Should().Be(3);
        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/compliance/erase");
    }
}
