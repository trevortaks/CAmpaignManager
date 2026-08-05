using System.Net;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.AfricasTalking;

/// <summary>Africa's Talking SMS API. Credentials: apiKey; settings: username, (optional) shortCode/fromNumber.</summary>
public sealed class AfricasTalkingSmsProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AfricasTalkingSmsProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.Sms;
    public string ProviderKey => "africas-talking";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Africa's Talking apiKey not configured.");
        }

        var username = credentials.Settings.GetValueOrDefault("username");
        if (string.IsNullOrEmpty(username))
        {
            return SendResult.TransientFailure("missing_settings", "Africa's Talking username not configured.");
        }

        var client = _httpClientFactory.CreateClient("africas-talking");
        var form = new Dictionary<string, string>
        {
            ["username"] = username,
            ["to"] = request.RecipientAddress,
            ["message"] = request.Body
        };
        var from = credentials.Settings.GetValueOrDefault("shortCode", request.Sender);
        if (!string.IsNullOrEmpty(from)) form["from"] = from;

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, "https://api.africastalking.com/version1/messaging")
        {
            Content = new FormUrlEncodedContent(form)
        };
        httpRequest.Headers.Add("apiKey", apiKey);
        httpRequest.Headers.Accept.ParseAdd("application/json");

        using var response = await client.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            return response.StatusCode == HttpStatusCode.BadRequest
                ? SendResult.Rejected(((int)response.StatusCode).ToString(), errorBody)
                : SendResult.TransientFailure(((int)response.StatusCode).ToString(), errorBody);
        }

        var body = await response.Content.ReadFromJsonAsync<AtResponse>(cancellationToken: ct);
        var recipient = body?.SMSMessageData?.Recipients?.FirstOrDefault();
        if (recipient is null)
        {
            return SendResult.TransientFailure("no_recipient_data", "Africa's Talking returned no recipient status.");
        }

        // Africa's Talking reports per-recipient status even on a 201 response.
        return recipient.Status?.Equals("Success", StringComparison.OrdinalIgnoreCase) == true
            ? SendResult.Ok(recipient.MessageId ?? $"at-{Guid.NewGuid():N}")
            : SendResult.Rejected(recipient.StatusCode?.ToString() ?? "rejected", recipient.Status ?? "Rejected");
    }

    /// <summary>Verifies credentials by fetching the account resource (no SMS sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Africa's Talking apiKey not configured.");
        }

        var username = credentials.Settings.GetValueOrDefault("username");
        if (string.IsNullOrEmpty(username))
        {
            return SendResult.TransientFailure("missing_settings", "Africa's Talking username not configured.");
        }

        var client = _httpClientFactory.CreateClient("africas-talking");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.africastalking.com/version1/user?username={Uri.EscapeDataString(username)}");
        httpRequest.Headers.Add("apiKey", apiKey);
        httpRequest.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(httpRequest, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record AtResponse(AtSmsData? SMSMessageData);
    private sealed record AtSmsData(List<AtRecipient>? Recipients);
    private sealed record AtRecipient(string? Status, int? StatusCode, string? MessageId);
}
