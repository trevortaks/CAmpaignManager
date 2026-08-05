using System.Net;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Meta;

/// <summary>WhatsApp via Meta Cloud API. Credentials: accessToken; settings: phoneNumberId.</summary>
public sealed class MetaWhatsAppProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MetaWhatsAppProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.WhatsApp;
    public string ProviderKey => "meta-whatsapp";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("accessToken", out var accessToken))
        {
            return SendResult.TransientFailure("missing_credentials", "Meta accessToken not configured.");
        }

        var phoneNumberId = credentials.Settings.GetValueOrDefault("phoneNumberId");
        if (string.IsNullOrEmpty(phoneNumberId))
        {
            return SendResult.TransientFailure("missing_settings", "Meta phoneNumberId not configured.");
        }

        var client = _httpClientFactory.CreateClient("meta-whatsapp");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"https://graph.facebook.com/v19.0/{phoneNumberId}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = request.RecipientAddress,
                type = "text",
                text = new { body = request.Body }
            })
        };
        httpRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<MetaSendResponse>(ct);
            var id = body?.Messages is { Count: > 0 } ? body.Messages[0].Id : "unknown";
            return SendResult.Ok(id ?? "unknown");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        return response.StatusCode == HttpStatusCode.BadRequest
            ? SendResult.Rejected("400", error)
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(), error);
    }

    /// <summary>Verifies the token by fetching the phone-number resource (no message sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("accessToken", out var accessToken))
        {
            return SendResult.TransientFailure("missing_credentials", "Meta accessToken not configured.");
        }

        var phoneNumberId = credentials.Settings.GetValueOrDefault("phoneNumberId");
        if (string.IsNullOrEmpty(phoneNumberId))
        {
            return SendResult.TransientFailure("missing_settings", "Meta phoneNumberId not configured.");
        }

        var client = _httpClientFactory.CreateClient("meta-whatsapp");
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://graph.facebook.com/v19.0/{phoneNumberId}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record MetaSendResponse(List<MetaMessageId>? Messages);
    private sealed record MetaMessageId(string? Id);
}
