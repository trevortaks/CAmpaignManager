using System.Net;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Infobip;

/// <summary>WhatsApp via Infobip's messages API. Credentials: apiKey; settings: baseUrl
/// (tenant-specific, e.g. "https://xxxxx.api.infobip.com").</summary>
public sealed class InfobipWhatsAppProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public InfobipWhatsAppProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.WhatsApp;
    public string ProviderKey => "infobip";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Infobip apiKey not configured.");
        }

        var baseUrl = credentials.Settings.GetValueOrDefault("baseUrl");
        if (string.IsNullOrEmpty(baseUrl))
        {
            return SendResult.TransientFailure("missing_settings", "Infobip baseUrl not configured.");
        }

        var client = _httpClientFactory.CreateClient("infobip");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/whatsapp/1/message/text")
        {
            Content = JsonContent.Create(new
            {
                from = request.Sender,
                to = request.RecipientAddress,
                content = new { text = request.Body }
            })
        };
        httpRequest.Headers.Add("Authorization", $"App {apiKey}");
        httpRequest.Headers.Accept.ParseAdd("application/json");

        using var response = await client.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<InfobipResponse>(cancellationToken: ct);
            var id = body?.Messages?.FirstOrDefault()?.MessageId;
            return SendResult.Ok(id ?? $"infobip-{Guid.NewGuid():N}");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        return response.StatusCode == HttpStatusCode.BadRequest
            ? SendResult.Rejected("400", error)
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(), error);
    }

    /// <summary>Verifies credentials by fetching the account balance (no message sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Infobip apiKey not configured.");
        }

        var baseUrl = credentials.Settings.GetValueOrDefault("baseUrl");
        if (string.IsNullOrEmpty(baseUrl))
        {
            return SendResult.TransientFailure("missing_settings", "Infobip baseUrl not configured.");
        }

        var client = _httpClientFactory.CreateClient("infobip");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/account/1/balance");
        httpRequest.Headers.Add("Authorization", $"App {apiKey}");
        httpRequest.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(httpRequest, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record InfobipResponse(List<InfobipMessage>? Messages);
    private sealed record InfobipMessage(string? MessageId);
}
