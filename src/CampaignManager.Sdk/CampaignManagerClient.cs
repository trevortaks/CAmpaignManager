using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Clients;
using CampaignManager.Sdk.Resilience;

namespace CampaignManager.Sdk;

/// <summary>Entry point for the CampaignManager API. Exposes one typed sub-client per resource
/// area. Note: webhooks (<c>api/webhooks/{providerKey}</c>) are server-inbound only — providers
/// call CampaignManager, not the other way round — so there is no outbound client for them.</summary>
public sealed class CampaignManagerClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IDisposable? _ownedCredential;
    private readonly bool _ownsHttpClient;
    public AuthClient Auth { get; }
    public CampaignsClient Campaigns { get; }
    public CampaignSeriesClient CampaignSeries { get; }
    public TemplatesClient Templates { get; }
    public ComplianceClient Compliance { get; }

    /// <summary>DI construction path: used by the typed-client factory registered via
    /// <c>AddCampaignManagerClient</c>. <paramref name="httpClient"/> is expected to already have
    /// its <see cref="HttpClient.BaseAddress"/>, auth handler and retry policy configured.</summary>
    public CampaignManagerClient(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    internal CampaignManagerClient(HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        Auth = new AuthClient(httpClient);
        Campaigns = new CampaignsClient(httpClient);
        CampaignSeries = new CampaignSeriesClient(httpClient);
        Templates = new TemplatesClient(httpClient);
        Compliance = new ComplianceClient(httpClient);
    }

    /// <summary>Non-DI construction path for scripts/console apps: builds its own
    /// <see cref="HttpClient"/> and handler chain (auth -&gt; optional retry -&gt;
    /// <paramref name="options"/>.PrimaryHandler or the default transport).</summary>
    public CampaignManagerClient(CampaignManagerClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _httpClient = BuildHttpClient(options);
        _ownsHttpClient = true;
        _ownedCredential = options.Credential as IDisposable;
        Auth = new AuthClient(_httpClient);
        Campaigns = new CampaignsClient(_httpClient);
        CampaignSeries = new CampaignSeriesClient(_httpClient);
        Templates = new TemplatesClient(_httpClient);
        Compliance = new ComplianceClient(_httpClient);
    }

    /// <summary>Common non-DI construction path using API-key authentication.</summary>
    public CampaignManagerClient(Uri baseAddress, string apiKey)
        : this(new CampaignManagerClientOptions
        {
            BaseAddress = baseAddress,
            Credential = new ApiKeyCredential(apiKey)
        })
    {
    }

    private static HttpClient BuildHttpClient(CampaignManagerClientOptions options)
    {
        HttpMessageHandler handler = options.PrimaryHandler ?? new SocketsHttpHandler();
        if (options.EnableRetries)
        {
            handler = new RetryHandler(options, handler);
        }

        handler = new AuthDelegatingHandler(options.Credential!) { InnerHandler = handler };

        return new HttpClient(handler)
        {
            BaseAddress = options.BaseAddress!,
            Timeout = options.Timeout
        };
    }

    /// <summary>Disposes resources created by the options/API-key constructors. An externally
    /// supplied <see cref="HttpClient"/> remains owned by its caller.</summary>
    public void Dispose()
    {
        if (!_ownsHttpClient) return;
        _httpClient.Dispose();
        _ownedCredential?.Dispose();
    }
}
