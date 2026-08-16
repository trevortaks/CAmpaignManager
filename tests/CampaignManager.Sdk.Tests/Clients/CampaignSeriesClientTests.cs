using System.Net;
using CampaignManager.Sdk.Models.Series;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Clients;

public sealed class CampaignSeriesClientTests
{
    private static SaveCampaignSeriesInput SampleInput() => new()
    {
        Name = "Weekly digest",
        Channel = CampaignChannel.Email,
        Sender = "digest@acme.test",
        CronExpression = "0 9 * * MON",
        Recipients = [new SeriesRecipientInput("someone@example.com", null)]
    };

    [Fact]
    public async Task CreateAsync_posts_and_unwraps_seriesId_envelope()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.Created,
            """{"seriesId":"11111111-1111-1111-1111-111111111111"}"""));
        var client = TestClientFactory.Create(stub);

        var id = await client.CampaignSeries.CreateAsync(SampleInput());

        id.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        stub.Requests[0].Method.Should().Be(HttpMethod.Post);
        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/campaign-series");
    }

    [Fact]
    public async Task PauseAsync_and_ResumeAsync_hit_the_expected_routes()
    {
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = TestClientFactory.Create(stub);
        var seriesId = Guid.NewGuid();

        await client.CampaignSeries.PauseAsync(seriesId);
        await client.CampaignSeries.ResumeAsync(seriesId);

        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be($"/api/campaign-series/{seriesId}/pause");
        stub.Requests[1].RequestUri!.PathAndQuery.Should().Be($"/api/campaign-series/{seriesId}/resume");
    }

    [Fact]
    public async Task DeleteAsync_sends_DELETE()
    {
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = TestClientFactory.Create(stub);

        await client.CampaignSeries.DeleteAsync(Guid.NewGuid());

        stub.Requests[0].Method.Should().Be(HttpMethod.Delete);
    }
}
