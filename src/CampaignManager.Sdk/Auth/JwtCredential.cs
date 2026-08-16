using System.Net.Http.Json;
using CampaignManager.Sdk.Internal;
using CampaignManager.Sdk.Models.Auth;

namespace CampaignManager.Sdk.Auth;

/// <summary>Authenticates via a JWT bearer token obtained from <c>POST api/auth/token</c>. The
/// token is cached and refreshed automatically shortly before it expires; concurrent requests
/// share a single in-flight refresh. Own its own unauthenticated <see cref="HttpClient"/> to fetch
/// tokens — it cannot reuse the main pipeline's <see cref="AuthDelegatingHandler"/>, which depends
/// on this credential already having a token (chicken-and-egg).</summary>
public sealed class JwtCredential : ICampaignManagerCredential, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

    private readonly Func<CancellationToken, Task<TokenResponse>> _tokenProvider;
    private readonly HttpClient? _ownedHttpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private TokenResponse? _cached;

    /// <summary>Fetches tokens by POSTing email/password to <paramref name="baseAddress"/>'s
    /// <c>api/auth/token</c> endpoint.</summary>
    public JwtCredential(Uri baseAddress, string email, string password)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!baseAddress.IsAbsoluteUri ||
            (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Base address must be an absolute HTTP or HTTPS URI.", nameof(baseAddress));
        }

        var httpClient = new HttpClient { BaseAddress = baseAddress };
        _ownedHttpClient = httpClient;
        _tokenProvider = async ct =>
        {
            using var response = await httpClient.PostAsJsonAsync(
                "api/auth/token", new TokenRequest { Email = email, Password = password },
                SdkJsonOptions.Default, ct)
                .ConfigureAwait(false);
            await ApiClientBase.EnsureSuccessAsync(response, ct).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<TokenResponse>(SdkJsonOptions.Default, ct)
                .ConfigureAwait(false))!;
        };
    }

    /// <summary>Escape hatch: supply your own token-fetch logic (e.g. pulling a token from a
    /// secrets manager) instead of embedding credentials in code. The SDK still caches and
    /// refreshes on your behalf using the <see cref="TokenResponse.ExpiresAtUtc"/> you return.</summary>
    public JwtCredential(Func<CancellationToken, Task<TokenResponse>> tokenProvider)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    public async ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!NeedsRefresh(_cached))
        {
            return _cached!.AccessToken;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (NeedsRefresh(_cached))
            {
                _cached = await _tokenProvider(cancellationToken).ConfigureAwait(false);
            }

            return _cached!.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool NeedsRefresh(TokenResponse? cached) =>
        cached is null || cached.ExpiresAtUtc - RefreshSkew <= DateTime.UtcNow;

    public void Dispose()
    {
        _ownedHttpClient?.Dispose();
        _refreshLock.Dispose();
    }
}
