using System.Net.Http.Json;
using System.Text.Json;
using CampaignManager.Sdk.Internal;
using CampaignManager.Sdk.Models.Series;

namespace CampaignManager.Sdk.Clients;

/// <summary>Wraps <c>api/campaign-series</c> (recurring campaign definitions).</summary>
public sealed class CampaignSeriesClient : ApiClientBase
{
    internal CampaignSeriesClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>GET api/campaign-series.</summary>
    public async Task<IReadOnlyList<CampaignSeriesSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/campaign-series");
        return await SendAsync<IReadOnlyList<CampaignSeriesSummary>>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET api/campaign-series/{seriesId}.</summary>
    public async Task<SaveCampaignSeriesInput> GetAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/campaign-series/{seriesId}");
        return await SendAsync<SaveCampaignSeriesInput>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/campaign-series.</summary>
    public async Task<Guid> CreateAsync(SaveCampaignSeriesInput input, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/campaign-series")
        {
            Content = JsonContent.Create(input, options: SdkJsonOptions.Default)
        };
        var envelope = await SendAsync<JsonElement>(request, cancellationToken).ConfigureAwait(false);
        return envelope.GetProperty("seriesId").GetGuid();
    }

    /// <summary>PUT api/campaign-series/{seriesId}.</summary>
    public async Task UpdateAsync(Guid seriesId, SaveCampaignSeriesInput input, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/campaign-series/{seriesId}")
        {
            Content = JsonContent.Create(input, options: SdkJsonOptions.Default)
        };
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/campaign-series/{seriesId}/pause.</summary>
    public async Task PauseAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/campaign-series/{seriesId}/pause");
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/campaign-series/{seriesId}/resume.</summary>
    public async Task ResumeAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/campaign-series/{seriesId}/resume");
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>DELETE api/campaign-series/{seriesId}.</summary>
    public async Task DeleteAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/campaign-series/{seriesId}");
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
