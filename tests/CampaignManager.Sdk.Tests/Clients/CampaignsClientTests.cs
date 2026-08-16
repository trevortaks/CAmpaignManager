using System.Net;
using CampaignManager.Sdk.Exceptions;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Clients;

public sealed class CampaignsClientTests
{
    [Fact]
    public async Task CreateAsync_posts_to_api_campaigns_and_deserializes_response()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.Accepted,
            """{"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","status":"Queued"}"""));
        var client = TestClientFactory.Create(stub);

        var result = await client.Campaigns.CreateAsync(new CreateCampaignRequest
        {
            Name = "Test",
            Channel = CampaignChannel.Sms,
            Sender = "ACME",
            MessageBody = "hi",
            Recipients = [new CampaignRecipientDto { Address = "+15551234567" }]
        });

        stub.Requests.Should().ContainSingle();
        stub.Requests[0].Method.Should().Be(HttpMethod.Post);
        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/campaigns");
        result.TrackingId.Should().Be("CMP-1");
        result.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task GetAsync_deserializes_camelCase_response_into_PascalCase_model()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK, """
            {"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","name":"Test",
             "channel":"Sms","status":"Sent","statistics":{"total":2,"sent":2},"failureReasons":[]}
            """));
        var client = TestClientFactory.Create(stub);

        var result = await client.Campaigns.GetAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        result.Status.Should().Be("Sent");
        result.Statistics.Total.Should().Be(2);
        result.Statistics.Sent.Should().Be(2);
        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/campaigns/11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task SearchAsync_builds_query_string_from_options()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK,
            """{"items":[],"page":2,"pageSize":10,"totalCount":0}"""));
        var client = TestClientFactory.Create(stub);

        await client.Campaigns.SearchAsync(new CampaignSearchOptions { Status = "Sent", Page = 2, PageSize = 10 });

        var uri = stub.Requests[0].RequestUri!;
        uri.PathAndQuery.Should().Contain("status=Sent").And.Contain("page=2").And.Contain("pageSize=10");
        uri.PathAndQuery.Should().NotContain("search=").And.NotContain("channel=");
    }

    [Fact]
    public async Task SearchAllAsync_pages_through_all_results()
    {
        var responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(
        [
            _ => TestClientFactory.Json(HttpStatusCode.OK,
                """{"items":[{"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","name":"A","channel":"Sms","status":"Sent","totalRecipients":1,"createdAtUtc":"2026-01-01T00:00:00Z"}],"page":1,"pageSize":1,"totalCount":2}"""),
            _ => TestClientFactory.Json(HttpStatusCode.OK,
                """{"items":[{"campaignId":"22222222-2222-2222-2222-222222222222","trackingId":"CMP-2","name":"B","channel":"Sms","status":"Sent","totalRecipients":1,"createdAtUtc":"2026-01-01T00:00:00Z"}],"page":2,"pageSize":1,"totalCount":2}""")
        ]);
        var stub = new StubHttpMessageHandler(responses);
        var client = TestClientFactory.Create(stub);

        var results = new List<CampaignSummaryResponse>();
        await foreach (var item in client.Campaigns.SearchAllAsync(new CampaignSearchOptions { PageSize = 1 }))
        {
            results.Add(item);
        }

        results.Should().HaveCount(2);
        results.Select(r => r.TrackingId).Should().Equal("CMP-1", "CMP-2");
    }

    [Fact]
    public async Task Non_success_response_throws_CampaignManagerApiException_with_validation_errors()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.BadRequest, """
            {"title":"Validation failed","status":400,"detail":"Name is required",
             "errors":{"Name":["Name is required"]}}
            """));
        var client = TestClientFactory.Create(stub);

        var act = () => client.Campaigns.GetAsync(Guid.NewGuid());

        var exception = await act.Should().ThrowAsync<CampaignManagerApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.ProblemTitle.Should().Be("Validation failed");
        exception.Which.ValidationErrors.Should().ContainKey("Name");
    }

    [Fact]
    public async Task Non_JSON_error_body_still_throws_with_status_code_only()
    {
        var stub = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var client = TestClientFactory.Create(stub);

        var act = () => client.Campaigns.GetAsync(Guid.NewGuid());

        var exception = await act.Should().ThrowAsync<CampaignManagerApiException>();
        exception.Which.StatusCode.Should().Be(429);
        exception.Which.ProblemDetails.Should().BeNull();
    }
}
