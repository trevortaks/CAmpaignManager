using System.Net;
using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Models.Common;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests;

public sealed class RequestLifetimeTests
{
    [Fact]
    public async Task Request_content_remains_readable_until_delayed_send_completes()
    {
        var handler = new DelayedContentHandler();
        using var client = new CampaignManagerClient(new CampaignManagerClientOptions
        {
            BaseAddress = new Uri("https://campaigns.test/"),
            Credential = new ApiKeyCredential("cmk_test"),
            PrimaryHandler = handler,
            EnableRetries = false
        });

        await client.Campaigns.CreateAsync(new CreateCampaignRequest
        {
            Name = "delayed", Channel = CampaignChannel.Sms, Sender = "ACME", MessageBody = "hello",
            Recipients = [new CampaignRecipientDto { Address = "+15551234567" }]
        });

        handler.Body.Should().Contain("delayed");
    }

    private sealed class DelayedContentHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(30, cancellationToken);
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """{"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","status":"Queued"}""")
            };
        }
    }
}
