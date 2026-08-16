namespace CampaignManager.Sdk.Auth;

/// <summary>Authenticates via a static API key, sent as the "X-Api-Key" header (matches the
/// server's ApiKeyAuthenticationOptions.HeaderName). Simple and stateless — no network calls.</summary>
public sealed class ApiKeyCredential : ICampaignManagerCredential
{
    private const string HeaderName = "X-Api-Key";
    private readonly string _apiKey;

    public ApiKeyCredential(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _apiKey = apiKey;
    }

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.Add(HeaderName, _apiKey);
        return ValueTask.CompletedTask;
    }
}
