using System.Net;
using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Resilience;

public sealed class RetryPipelineTests
{
    [Fact]
    public async Task Get_retries_transient_failure_and_succeeds()
    {
        var attempts = 0;
        var client = Create(new StubHttpMessageHandler(_ => ++attempts < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : TestClientFactory.Json(HttpStatusCode.OK, CampaignJson)));

        var result = await client.Campaigns.GetAsync(Guid.NewGuid());

        result.Status.Should().Be("Sent");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Campaign_creation_post_is_attempted_once_by_default()
    {
        var attempts = 0;
        var client = Create(new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }));

        var act = () => client.Campaigns.CreateAsync(new CreateCampaignRequest
        {
            Name = "one", Channel = CampaignChannel.Sms, Sender = "ACME", MessageBody = "hi",
            Recipients = [new CampaignRecipientDto { Address = "+15551234567" }]
        });

        await act.Should().ThrowAsync<CampaignManager.Sdk.Exceptions.CampaignManagerApiException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task Ordinary_4xx_is_not_retried()
    {
        var attempts = 0;
        var client = Create(new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }));

        await FluentActions.Awaiting(() => client.Campaigns.GetAsync(Guid.NewGuid()))
            .Should().ThrowAsync<CampaignManager.Sdk.Exceptions.CampaignManagerApiException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task Maximum_retry_attempts_are_respected()
    {
        var attempts = 0;
        var client = Create(new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
        }), maxRetries: 2);

        await FluentActions.Awaiting(() => client.Campaigns.GetAsync(Guid.NewGuid()))
            .Should().ThrowAsync<CampaignManager.Sdk.Exceptions.CampaignManagerApiException>();
        attempts.Should().Be(3);
    }

    private static CampaignManagerClient Create(HttpMessageHandler handler, int maxRetries = 3) =>
        new(new CampaignManagerClientOptions
        {
            BaseAddress = new Uri("https://campaigns.test/"),
            Credential = new ApiKeyCredential("cmk_test"),
            PrimaryHandler = handler,
            MaxRetryAttempts = maxRetries,
            RetryBaseDelay = TimeSpan.Zero
        });

    private const string CampaignJson = """
        {"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","name":"Test",
         "channel":"Sms","status":"Sent","statistics":{"total":1,"sent":1},"failureReasons":[]}
        """;
}
