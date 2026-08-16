namespace CampaignManager.Sdk.Models.Auth;

/// <summary>Credentials for <c>POST api/auth/token</c>. Mirrors CampaignManager.Contracts.Auth.TokenRequest.</summary>
public sealed class TokenRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

/// <summary>Mirrors CampaignManager.Contracts.Auth.TokenResponse.</summary>
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc);
