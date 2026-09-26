using System.Net;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Gikko;

/// <summary>Gikko SMS via its Infobip-backed REST API. Credentials: apiKey; settings: baseUrl, fromNumber.</summary>
public sealed class GikkoSmsProvider(IHttpClientFactory httpClientFactory) : IChannelProvider, ITestableProvider
{
    public Channel Channel => Channel.Sms;
    public string ProviderKey => "gikko";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
            return SendResult.TransientFailure("missing_credentials", "Gikko apiKey not configured.");

        var baseUrl = credentials.Settings.GetValueOrDefault("baseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return SendResult.TransientFailure("missing_settings", "Gikko baseUrl not configured.");

        var sender = credentials.Settings.GetValueOrDefault("fromNumber") ?? request.Sender;
        var client = httpClientFactory.CreateClient("gikko");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/sms/3/messages")
        {
            Content = JsonContent.Create(new
            {
                messages = new[]
                {
                    new
                    {
                        sender,
                        destinations = new[] { new { to = request.RecipientAddress } },
                        content = new { text = request.Body }
                    }
                }
            })
        };
        httpRequest.Headers.Add("Authorization", $"App {apiKey}");
        httpRequest.Headers.Accept.ParseAdd("application/json");

        using var response = await client.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return response.StatusCode == HttpStatusCode.BadRequest
                ? SendResult.Rejected("400", error)
                : SendResult.TransientFailure(((int)response.StatusCode).ToString(), error);
        }

        var body = await response.Content.ReadFromJsonAsync<GikkoResponse>(cancellationToken: ct);
        var messageId = body?.Messages?.FirstOrDefault()?.MessageId;
        return string.IsNullOrWhiteSpace(messageId)
            ? SendResult.TransientFailure("no_message_id", "Gikko returned no message ID.")
            : SendResult.Ok(messageId);
    }

    /// <summary>Verifies the API key by fetching the account balance (no SMS sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
            return SendResult.TransientFailure("missing_credentials", "Gikko apiKey not configured.");

        var baseUrl = credentials.Settings.GetValueOrDefault("baseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return SendResult.TransientFailure("missing_settings", "Gikko baseUrl not configured.");

        var client = httpClientFactory.CreateClient("gikko");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/account/1/balance");
        request.Headers.Add("Authorization", $"App {apiKey}");
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record GikkoResponse(List<GikkoMessage>? Messages);
    private sealed record GikkoMessage(string? MessageId);
}
