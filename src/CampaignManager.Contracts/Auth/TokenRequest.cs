namespace CampaignManager.Contracts.Auth;

public sealed class TokenRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc);
