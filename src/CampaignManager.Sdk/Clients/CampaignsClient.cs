using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using CampaignManager.Sdk.Internal;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Models.Campaigns.Import;
using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Clients;

/// <summary>Wraps <c>api/campaigns</c> (create/get/cancel/search) and the CSV import endpoint.</summary>
public sealed class CampaignsClient : ApiClientBase
{
    internal CampaignsClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>POST api/campaigns. Validation is synchronous; sending happens in the background.
    /// This non-idempotent operation is not retried by default.</summary>
    public async Task<CreateCampaignResponse> CreateAsync(
        CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/campaigns")
        {
            Content = JsonContent.Create(request, options: SdkJsonOptions.Default)
        };
        return await SendAsync<CreateCampaignResponse>(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET api/campaigns/{campaignId}.</summary>
    public async Task<CampaignStatusResponse> GetAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/campaigns/{campaignId}");
        return await SendAsync<CampaignStatusResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET api/campaigns/by-tracking/{trackingId}.</summary>
    public async Task<CampaignStatusResponse> GetByTrackingIdAsync(
        string trackingId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"api/campaigns/by-tracking/{Uri.EscapeDataString(trackingId)}");
        return await SendAsync<CampaignStatusResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/campaigns/{campaignId}/cancel.</summary>
    public async Task CancelAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/campaigns/{campaignId}/cancel");
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET api/campaigns — a single page of results.</summary>
    public async Task<PagedResult<CampaignSummaryResponse>> SearchAsync(
        CampaignSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Page < 1) throw new ArgumentOutOfRangeException(nameof(options), "Page must be at least 1.");
        if (options.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(options), "Page size must be between 1 and 100.");
        var query = new QueryStringBuilder()
            .Add("search", options.Search)
            .Add("status", options.Status)
            .Add("channel", options.Channel?.ToString())
            .Add("fromUtc", options.FromUtc)
            .Add("toUtc", options.ToUtc)
            .Add("page", options.Page)
            .Add("pageSize", options.PageSize);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/campaigns{query}");
        return await SendAsync<PagedResult<CampaignSummaryResponse>>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Convenience helper: repeatedly calls <see cref="SearchAsync"/>, advancing the page,
    /// and yields every result across all pages. Not a server endpoint — sugar over
    /// <see cref="SearchAsync"/>.</summary>
    public async IAsyncEnumerable<CampaignSummaryResponse> SearchAllAsync(
        CampaignSearchOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = options.Page > 0 ? options.Page : 1;
        while (true)
        {
            var result = await SearchAsync(options with { Page = page }, cancellationToken).ConfigureAwait(false);
            foreach (var item in result.Items)
            {
                yield return item;
            }

            if (result.Items.Count == 0 || page >= result.TotalPages) yield break;
            page++;
        }
    }

    /// <summary>POST api/campaigns/import (multipart CSV upload). Lives here rather than on a
    /// separate client since it's a sibling of <see cref="CreateAsync"/> sharing the same route.</summary>
    public async Task<ImportResult> ImportAsync(
        ImportCampaignRequest request, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(request.Name), "name" },
            { new StringContent(request.Channel.ToString()), "channel" },
            { new StringContent(request.Sender), "sender" }
        };

        var fileContent = new StreamContent(request.OwnsFileContent
            ? request.FileContent
            : new NonDisposingStream(request.FileContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType);
        content.Add(fileContent, "file", request.FileName);

        if (request.MessageBody is not null) content.Add(new StringContent(request.MessageBody), "messageBody");
        if (request.TemplateId is { } templateId) content.Add(new StringContent(templateId.ToString()), "templateId");
        if (request.Subject is not null) content.Add(new StringContent(request.Subject), "subject");
        if (request.ScheduledAtUtc is { } scheduledAtUtc)
        {
            content.Add(new StringContent(scheduledAtUtc.ToString("O")), "scheduledAtUtc");
        }
        if (request.CallbackUrl is not null) content.Add(new StringContent(request.CallbackUrl), "callbackUrl");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/campaigns/import") { Content = content };
        return await SendAsync<ImportResult>(httpRequest, cancellationToken).ConfigureAwait(false);
    }
}
