using System.Net;
using System.Text;
using CampaignManager.Sdk.Models.Campaigns.Import;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Clients;

public sealed class CampaignsClientImportTests
{
    [Fact]
    public async Task ImportAsync_sends_multipart_fields_matching_the_controllers_FromForm_names()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.Accepted, """
            {"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","status":"Queued",
             "acceptedRecipients":2,"invalidRows":0,"duplicateRows":0,"errors":[]}
            """));
        var client = TestClientFactory.Create(stub);
        var csv = Encoding.UTF8.GetBytes("address\n+15551234567\n+15551234568\n");

        var result = await client.Campaigns.ImportAsync(ImportCampaignRequest.FromBytes(
            csv, "recipients.csv", "Import test", CampaignChannel.Sms, "ACME",
            subject: null, scheduledAtUtc: DateTime.UtcNow, callbackUrl: "https://hooks.example/cb"));

        var request = stub.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.PathAndQuery.Should().Be("/api/campaigns/import");
        var body = stub.RequestBodies[0]!;

        // [FromForm] field names on CampaignImportController.Import — exact match is load-bearing.
        body.Should().Contain("name=name").And.Contain("Import test");
        body.Should().Contain("name=channel").And.Contain("Sms");
        body.Should().Contain("name=sender").And.Contain("ACME");
        body.Should().Contain("name=file; filename=recipients.csv");
        body.Should().Contain("name=scheduledAtUtc");
        body.Should().Contain("name=callbackUrl").And.Contain("https://hooks.example/cb");
        body.Should().NotContain("name=subject", "Subject was null and must be omitted, not sent empty");

        result.AcceptedRecipients.Should().Be(2);
    }
}
