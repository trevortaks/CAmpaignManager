using System.Net;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Twilio;

/// <summary>WhatsApp via Twilio's Messages API (same endpoint as SMS, "whatsapp:" prefixed
/// addresses). Credentials: accountSid, authToken; settings: fromNumber (E.164, without prefix).</summary>
public sealed class TwilioWhatsAppProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TwilioWhatsAppProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.WhatsApp;
    public string ProviderKey => "twilio-whatsapp";

    private static string WithPrefix(string address) =>
        address.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? address : $"whatsapp:{address}";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("accountSid", out var accountSid) ||
            !credentials.Secrets.TryGetValue("authToken", out var authToken))
        {
            return SendResult.TransientFailure("missing_credentials", "Twilio accountSid/authToken not configured.");
        }

        var from = credentials.Settings.GetValueOrDefault("fromNumber", request.Sender);
        var client = _httpClientFactory.CreateClient("twilio-whatsapp");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = WithPrefix(request.RecipientAddress),
                ["From"] = WithPrefix(from),
                ["Body"] = request.Body
            })
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));

        using var response = await client.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TwilioMessageResponse>(cancellationToken: ct);
            return SendResult.Ok(body?.Sid ?? "unknown");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
            ? SendResult.Rejected(((int)response.StatusCode).ToString(), error)
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(), error);
    }

    /// <summary>Verifies credentials by fetching the account resource (no message sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("accountSid", out var accountSid) ||
            !credentials.Secrets.TryGetValue("authToken", out var authToken))
        {
            return SendResult.TransientFailure("missing_credentials", "Twilio accountSid/authToken not configured.");
        }

        var client = _httpClientFactory.CreateClient("twilio-whatsapp");
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}.json");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));
        using var response = await client.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record TwilioMessageResponse(string? Sid);
}
