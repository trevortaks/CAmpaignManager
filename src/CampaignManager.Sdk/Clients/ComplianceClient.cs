using System.Net.Http.Json;
using System.Text.Json;
using CampaignManager.Sdk.Internal;
using CampaignManager.Sdk.Models.Compliance;
using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Clients;

/// <summary>Wraps <c>api/compliance</c> (suppression list + right-to-erasure).</summary>
public sealed class ComplianceClient : ApiClientBase
{
    internal ComplianceClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>GET api/compliance/suppressions.</summary>
    public async Task<IReadOnlyList<SuppressionSummary>> ListSuppressionsAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/compliance/suppressions");
        return await SendAsync<IReadOnlyList<SuppressionSummary>>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/compliance/suppressions. An address already suppressed on the same
    /// channel is not duplicated — the server returns the existing suppression's id.</summary>
    public async Task<Guid> SuppressAsync(
        string address, CampaignChannel? channel = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/compliance/suppressions")
        {
            Content = JsonContent.Create(new { address, channel, reason }, options: SdkJsonOptions.Default)
        };
        var envelope = await SendAsync<JsonElement>(request, cancellationToken).ConfigureAwait(false);
        return envelope.GetProperty("suppressionId").GetGuid();
    }

    /// <summary>DELETE api/compliance/suppressions/{id}.</summary>
    public async Task RemoveSuppressionAsync(Guid suppressionId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/compliance/suppressions/{suppressionId}");
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/compliance/erase — right-to-erasure. Redacts every stored copy of the
    /// address's PII in the tenant's campaign/series recipient lists; does not remove suppression
    /// rows, so an erased address is still never re-contacted. Returns the number of rows redacted.</summary>
    public async Task<int> EraseAsync(string address, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/compliance/erase")
        {
            Content = JsonContent.Create(new { address }, options: SdkJsonOptions.Default)
        };
        var envelope = await SendAsync<JsonElement>(request, cancellationToken).ConfigureAwait(false);
        return envelope.GetProperty("rowsRedacted").GetInt32();
    }
}
