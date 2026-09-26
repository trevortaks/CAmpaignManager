using System.Net;
using System.Text;
using System.Text.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Infrastructure.Providers.Gikko;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CampaignManager.UnitTests.Providers;

public sealed class GikkoSmsProviderTests
{
    [Fact]
    public async Task Sends_using_gikko_sms_contract_and_returns_message_id()
    {
        var handler = new RecordingHandler("""
            {"messages":[{"messageId":"gikko-message-1"}]}
            """);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("gikko").Returns(new HttpClient(handler));
        var provider = new GikkoSmsProvider(factory);

        var result = await provider.SendAsync(
            new ProviderSendRequest(Guid.NewGuid(), "263771234567", "Hello", null, "CampaignSender",
                new Dictionary<string, string>()),
            new ProviderCredentials(
                new Dictionary<string, string> { ["apiKey"] = "secret" },
                new Dictionary<string, string>
                {
                    ["baseUrl"] = "https://tenant.api.infobip.com/",
                    ["fromNumber"] = "GIKKO"
                }),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProviderMessageId.Should().Be("gikko-message-1");
        handler.Uri.Should().Be("https://tenant.api.infobip.com/sms/3/messages");
        handler.Authorization.Should().Be("App secret");
        using var body = JsonDocument.Parse(handler.Body!);
        var message = body.RootElement.GetProperty("messages")[0];
        message.GetProperty("sender").GetString().Should().Be("GIKKO");
        message.GetProperty("destinations")[0].GetProperty("to").GetString().Should().Be("263771234567");
        message.GetProperty("content").GetProperty("text").GetString().Should().Be("Hello");
    }

    [Fact]
    public async Task Rejects_bad_requests_without_retrying()
    {
        var handler = new RecordingHandler("invalid destination", HttpStatusCode.BadRequest);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("gikko").Returns(new HttpClient(handler));
        var provider = new GikkoSmsProvider(factory);

        var result = await provider.SendAsync(
            new ProviderSendRequest(Guid.NewGuid(), "invalid", "Hello", null, "Sender",
                new Dictionary<string, string>()),
            new ProviderCredentials(
                new Dictionary<string, string> { ["apiKey"] = "secret" },
                new Dictionary<string, string> { ["baseUrl"] = "https://tenant.api.infobip.com" }),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("400");
    }

    private sealed class RecordingHandler(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string? Uri { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
