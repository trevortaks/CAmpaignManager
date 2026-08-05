using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Mailgun;

/// <summary>Mailgun HTTP API. Credentials: apiKey; settings: domain.</summary>
public sealed class MailgunEmailProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MailgunEmailProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.Email;
    public string ProviderKey => "mailgun";

    private static AuthenticationHeaderValue BasicAuth(string apiKey) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{apiKey}")));

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Mailgun apiKey not configured.");
        }

        var domain = credentials.Settings.GetValueOrDefault("domain");
        if (string.IsNullOrEmpty(domain))
        {
            return SendResult.TransientFailure("missing_settings", "Mailgun domain not configured.");
        }

        var client = _httpClientFactory.CreateClient("mailgun");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"https://api.mailgun.net/v3/{domain}/messages")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["from"] = request.Sender,
                ["to"] = request.RecipientAddress,
                ["subject"] = request.Subject ?? "(no subject)",
                ["text"] = request.Body
            })
        };
        httpRequest.Headers.Authorization = BasicAuth(apiKey);

        using var response = await client.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<MailgunSendResponse>(cancellationToken: ct);
            return SendResult.Ok(body?.Id ?? $"mailgun-{Guid.NewGuid():N}");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        return response.StatusCode is HttpStatusCode.BadRequest
            ? SendResult.Rejected(((int)response.StatusCode).ToString(), error)
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(), error);
    }

    /// <summary>Verifies credentials by fetching the domain resource (no email sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "Mailgun apiKey not configured.");
        }

        var domain = credentials.Settings.GetValueOrDefault("domain");
        if (string.IsNullOrEmpty(domain))
        {
            return SendResult.TransientFailure("missing_settings", "Mailgun domain not configured.");
        }

        var client = _httpClientFactory.CreateClient("mailgun");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.mailgun.net/v3/domains/{domain}");
        httpRequest.Headers.Authorization = BasicAuth(apiKey);
        using var response = await client.SendAsync(httpRequest, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }

    private sealed record MailgunSendResponse(string? Id, string? Message);
}
