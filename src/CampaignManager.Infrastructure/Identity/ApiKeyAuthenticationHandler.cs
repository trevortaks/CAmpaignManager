using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CampaignManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CampaignManager.Infrastructure.Identity;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

/// <summary>Authenticates external systems via the X-Api-Key header. Keys are looked up by
/// prefix and verified with a constant-time SHA-256 hash comparison.</summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly AppDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var presentedKey = headerValues.ToString();
        if (presentedKey.Length < 12)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var prefix = presentedKey[..12];
        var candidates = await _db.ApiKeys
            .AsNoTracking()
            .Where(k => k.KeyPrefix == prefix)
            .ToListAsync(Context.RequestAborted);

        var presentedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey)));
        var utcNow = DateTime.UtcNow;
        foreach (var candidate in candidates)
        {
            if (!candidate.IsActive(utcNow)) continue;
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate.KeyHash),
                    Encoding.ASCII.GetBytes(presentedHash)))
            {
                continue;
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, $"apikey:{candidate.Id}"),
                new Claim(JwtTokenService.OrganizationClaim, candidate.OrganizationId.ToString()),
                new Claim(ClaimTypes.Role, "ApiClient")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        return AuthenticateResult.Fail("Invalid API key.");
    }
}
