namespace CampaignManager.Sdk.Auth;

/// <summary>Applies the configured <see cref="ICampaignManagerCredential"/> to every outgoing
/// request before it is sent.</summary>
public sealed class AuthDelegatingHandler : DelegatingHandler
{
    private readonly ICampaignManagerCredential _credential;

    public AuthDelegatingHandler(ICampaignManagerCredential credential)
    {
        _credential = credential;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _credential.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
