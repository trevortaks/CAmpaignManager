using System.Net;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests;

public sealed class PaginationTests
{
    [Fact]
    public async Task SearchAllAsync_returns_nothing_for_a_zero_result_search()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK,
            """{"items":[],"page":1,"pageSize":20,"totalCount":0}"""));
        var client = TestClientFactory.Create(stub);

        var results = new List<CampaignSummaryResponse>();
        await foreach (var item in client.Campaigns.SearchAllAsync(new CampaignSearchOptions()))
        {
            results.Add(item);
        }

        results.Should().BeEmpty();
        stub.Requests.Should().ContainSingle("a single empty page should stop the loop immediately");
    }

    [Fact]
    public async Task SearchAllAsync_stops_after_a_single_page_that_covers_everything()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK, """
            {"items":[{"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","name":"A",
             "channel":"Sms","status":"Sent","totalRecipients":1,"createdAtUtc":"2026-01-01T00:00:00Z"}],
             "page":1,"pageSize":20,"totalCount":1}
            """));
        var client = TestClientFactory.Create(stub);

        var results = new List<CampaignSummaryResponse>();
        await foreach (var item in client.Campaigns.SearchAllAsync(new CampaignSearchOptions()))
        {
            results.Add(item);
        }

        results.Should().ContainSingle();
        stub.Requests.Should().ContainSingle();
    }
}
