namespace CampaignManager.Sdk.Auth;

/// <summary>Applies authentication to an outgoing request. Implementations: <see cref="ApiKeyCredential"/>
/// (static X-Api-Key header) and <see cref="JwtCredential"/> (bearer token, fetched/cached/refreshed
/// automatically). Both satisfy the API's "ApiAccess" policy — either works for every authenticated
/// endpoint.</summary>
public interface ICampaignManagerCredential
{
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
