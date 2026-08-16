using System.Net;
using System.Text;
using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Models.Campaigns.Import;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests;

public sealed class OwnershipTests
{
    [Fact]
    public void Options_client_disposes_its_handler_and_disposable_credential()
    {
        var handler = new TrackingHandler();
        var credential = new TrackingCredential();
        var client = new CampaignManagerClient(new CampaignManagerClientOptions
        {
            BaseAddress = new Uri("https://campaigns.test/"), Credential = credential,
            PrimaryHandler = handler, EnableRetries = false
        });

        client.Dispose();

        handler.Disposed.Should().BeTrue();
        credential.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Client_does_not_dispose_externally_supplied_HttpClient()
    {
        var handler = new TrackingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://campaigns.test/") };
        using (var client = new CampaignManagerClient(httpClient)) client.Dispose();

        using var response = await httpClient.GetAsync("health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Import_leaves_caller_stream_open()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("address\n+15551234567\n"));
        using var client = TestClientFactory.Create(ImportHandler());
        var request = new ImportCampaignRequest
        {
            Name = "import", Channel = CampaignChannel.Sms, Sender = "ACME",
            FileContent = stream, FileName = "recipients.csv"
        };

        await client.Campaigns.ImportAsync(request);

        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task FromFile_closes_its_owned_file_after_import()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "address\n+15551234567\n");
        var request = ImportCampaignRequest.FromFile(path, "import", CampaignChannel.Sms, "ACME");
        using var client = TestClientFactory.Create(ImportHandler());

        await client.Campaigns.ImportAsync(request);

        request.FileContent.CanRead.Should().BeFalse();
        File.Delete(path);
    }

    private static StubHttpMessageHandler ImportHandler() => new(_ => TestClientFactory.Json(
        HttpStatusCode.Accepted,
        """{"campaignId":"11111111-1111-1111-1111-111111111111","trackingId":"CMP-1","status":"Queued","acceptedRecipients":1,"invalidRows":0,"duplicateRows":0,"errors":[]}"""));

    private sealed class TrackingHandler : HttpMessageHandler
    {
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackingCredential : ICampaignManagerCredential, IDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public void Dispose() => Disposed = true;
    }
}
