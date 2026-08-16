using System.Net.Http.Json;
using CampaignManager.Sdk.Internal;
using CampaignManager.Sdk.Models.Auth;

namespace CampaignManager.Sdk.Clients;

/// <summary>Wraps <c>api/auth</c>. Exposed mainly so <see cref="Auth.JwtCredential"/> can fetch
/// tokens internally, but also usable directly by consumers who want to manage tokens themselves.</summary>
public sealed class AuthClient : ApiClientBase
{
    internal AuthClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>POST api/auth/token.</summary>
    public async Task<TokenResponse> GetTokenAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/token")
        {
            Content = JsonContent.Create(new TokenRequest { Email = email, Password = password }, options: SdkJsonOptions.Default)
        };
        return await SendAsync<TokenResponse>(request, cancellationToken).ConfigureAwait(false);
    }
}
