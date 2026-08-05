using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.SendGrid;

/// <summary>SendGrid Mail Send v3 API. Credentials: apiKey; settings: fromName (optional).</summary>
public sealed class SendGridEmailProvider : IChannelProvider, ITestableProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SendGridEmailProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Channel Channel => Channel.Email;
    public string ProviderKey => "sendgrid";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "SendGrid apiKey not configured.");
        }

        var client = _httpClientFactory.CreateClient("sendgrid");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Content = JsonContent.Create(new
            {
                personalizations = new[] { new { to = new[] { new { email = request.RecipientAddress } } } },
                from = new { email = request.Sender, name = credentials.Settings.GetValueOrDefault("fromName") },
                subject = request.Subject ?? "(no subject)",
                content = new[] { new { type = "text/plain", value = request.Body } }
            })
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await client.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            // SendGrid returns the message id in X-Message-Id, not the body.
            var messageId = response.Headers.TryGetValues("X-Message-Id", out var values)
                ? values.FirstOrDefault()
                : $"sendgrid-{Guid.NewGuid():N}";
            return SendResult.Ok(messageId ?? $"sendgrid-{Guid.NewGuid():N}");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
            ? SendResult.Rejected(((int)response.StatusCode).ToString(), error)
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(), error);
    }

    /// <summary>Verifies the API key by fetching account info (no email sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        if (!credentials.Secrets.TryGetValue("apiKey", out var apiKey))
        {
            return SendResult.TransientFailure("missing_credentials", "SendGrid apiKey not configured.");
        }

        var client = _httpClientFactory.CreateClient("sendgrid");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.sendgrid.com/v3/user/account");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.SendAsync(httpRequest, ct);
        return response.IsSuccessStatusCode
            ? SendResult.Ok("test-ok")
            : SendResult.TransientFailure(((int)response.StatusCode).ToString(),
                await response.Content.ReadAsStringAsync(ct));
    }
}
