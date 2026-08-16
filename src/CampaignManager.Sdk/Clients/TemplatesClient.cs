using System.Net.Http.Json;
using System.Text.Json;
using CampaignManager.Sdk.Internal;
using CampaignManager.Sdk.Models.Templates;

namespace CampaignManager.Sdk.Clients;

/// <summary>Wraps <c>api/templates</c>.</summary>
public sealed class TemplatesClient : ApiClientBase
{
    internal TemplatesClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>GET api/templates.</summary>
    public async Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/templates");
        return await SendAsync<IReadOnlyList<TemplateSummary>>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/templates.</summary>
    public async Task<Guid> CreateAsync(SaveTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/templates")
        {
            Content = JsonContent.Create(input, options: SdkJsonOptions.Default)
        };
        var envelope = await SendAsync<JsonElement>(request, cancellationToken).ConfigureAwait(false);
        return envelope.GetProperty("templateId").GetGuid();
    }

    /// <summary>PUT api/templates/{templateId}.</summary>
    public async Task UpdateAsync(Guid templateId, SaveTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/templates/{templateId}")
        {
            Content = JsonContent.Create(input, options: SdkJsonOptions.Default)
        };
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>DELETE api/templates/{templateId}.</summary>
    public async Task DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/templates/{templateId}");
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST api/templates/preview — renders a stored or ad-hoc template against sample data.</summary>
    public async Task<TemplatePreview> PreviewAsync(
        PreviewTemplateRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/templates/preview")
        {
            Content = JsonContent.Create(request, options: SdkJsonOptions.Default)
        };
        return await SendAsync<TemplatePreview>(httpRequest, cancellationToken).ConfigureAwait(false);
    }
}
