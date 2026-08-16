using System.Net.Http.Json;
using CampaignManager.Sdk.Exceptions;
using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Internal;

/// <summary>Shared send/deserialize/error-translate helpers for every resource sub-client, so the
/// try/parse/throw sequence isn't duplicated across ~20 client methods.</summary>
public abstract class ApiClientBase
{
    private protected readonly HttpClient HttpClient;

    private protected ApiClientBase(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    /// <summary>Sends the request and deserializes a JSON response body.</summary>
    private protected async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(SdkJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false))!;
    }

    /// <summary>Sends the request for endpoints that return no body (204, or 202 with no payload).</summary>
    private protected async Task SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        CampaignManagerProblemDetails? problem = null;
        try
        {
            problem = await response.Content
                .ReadFromJsonAsync<CampaignManagerProblemDetails>(SdkJsonOptions.Default, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Not every failure carries a JSON ProblemDetails body (e.g. 429 rate-limit
            // rejections, or a proxy/gateway error) — fall back to StatusCode-only.
        }

        throw new CampaignManagerApiException((int)response.StatusCode, problem);
    }
}
