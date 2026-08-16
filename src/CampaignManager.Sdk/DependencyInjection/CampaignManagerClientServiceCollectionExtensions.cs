using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace CampaignManager.Sdk.DependencyInjection;

public static class CampaignManagerClientServiceCollectionExtensions
{
    private const string ClientName = "CampaignManager.Sdk";
    /// <summary>Registers <see cref="CampaignManagerClient"/> as a typed <see cref="HttpClient"/>
    /// via <see cref="IHttpClientFactory"/>, with the configured credential applied to every
    /// request and (by default) transient failures retried with backoff. Returns the
    /// <see cref="IHttpClientBuilder"/> so callers can chain further customization.</summary>
    public static IHttpClientBuilder AddCampaignManagerClient(
        this IServiceCollection services, Action<CampaignManagerClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new CampaignManagerClientOptions();
        configureOptions(options);
        options.Validate();

        services.AddSingleton(options.Credential!);
        services.AddTransient<AuthDelegatingHandler>();

        var builder = services.AddHttpClient(ClientName, httpClient =>
        {
            httpClient.BaseAddress = options.BaseAddress;
            httpClient.Timeout = options.Timeout;
        }).AddHttpMessageHandler<AuthDelegatingHandler>();

        if (options.EnableRetries)
        {
            builder = builder.AddHttpMessageHandler(() => new RetryHandler(options));
        }

        services.AddTransient(sp => new CampaignManagerClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName), ownsHttpClient: true));

        return builder;
    }
}
