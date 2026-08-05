using System.Net;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Clickatell;

/// <summary>Clickatell messages REST API. Credentials: apiKey (the Clickatell "Authorization" API key).</summary>
public sealed class ClickatellSmsProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ClickatellSmsProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.Sms;
    public string ProviderKey => "clickatell";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Clickatell apiKey not configured.");
        }

        var client = _httpClientFactory.CreateClient("clickatell");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://platform.clickatell.com/messages")
        {
            Content = JsonContent.Create(new
            {
                content = request.Body,
                to = new[] { request.RecipientAddress }
            })
        };
        httpRequest.Headers.Add("Authorization", apiKey);
        httpRequest.Headers.Accept.ParseAdd("application/json");

        using var response = await client.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            return response.StatusCode == HttpStatusCode.BadRequest
                ? SendResult.Rejected(((int)response.StatusCode).ToString(), errorBody)
                : SendResult.TransientFailure(((int)response.StatusCode).ToString(), errorBody);
        }

        var body = await response.Content.ReadFromJsonAsync<ClickatellResponse>(cancellationToken: ct);
        var message = body?.Messages?.FirstOrDefault();
        if (message is null)
        {
            return SendResult.TransientFailure("no_message_data", "Clickatell returned no message status.");
        }

        return message.Accepted
            ? SendResult.Ok(message.ApiMessageId ?? $"clickatell-{Guid.NewGuid():N}")
            : SendResult.Rejected("not_accepted", "Clickatell did not accept the message for this recipient.");
    }

    /// <summary>Verifies the API key by fetching the account balance (no SMS sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Clickatell apiKey not configured.");
        }

        var client = _httpClientFactory.CreateClient("clickatell");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://platform.clickatell.com/account/balance");
        httpRequest.Headers.Add("Authorization", apiKey);
        httpRequest.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(httpRequest, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record ClickatellResponse(List<ClickatellMessage>? Messages);
    private sealed record ClickatellMessage(string? ApiMessageId, bool Accepted);
}
