using System.Security.Cryptography;
using System.Text;

namespace CampaignManager.Infrastructure.Security;

/// <summary>Verifies a provider-specific webhook signature over the raw request body.
/// Implementations are pure (no I/O) so they are unit-testable without provider accounts.</summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>Provider key this verifier applies to (e.g. "meta-whatsapp", "twilio").</summary>
    string ProviderKey { get; }

    bool Verify(string secret, string rawBody, string signatureHeader, string requestUrl);
}

/// <summary>Meta (WhatsApp Cloud API) style: X-Hub-Signature-256 = "sha256=" + HMACSHA256(appSecret, body).</summary>
public sealed class MetaWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    public string ProviderKey => "meta-whatsapp";

    public bool Verify(string secret, string rawBody, string signatureHeader, string requestUrl)
    {
        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(signatureHeader[prefix.Length..].ToLowerInvariant()));
    }
}

/// <summary>Twilio style: X-Twilio-Signature = Base64(HMACSHA1(authToken, url + sorted form params)).
/// For JSON callbacks the signed payload is url + body.</summary>
public sealed class TwilioWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    public string ProviderKey => "twilio";

    public bool Verify(string secret, string rawBody, string signatureHeader, string requestUrl)
    {
        var signed = requestUrl + rawBody;
        var expected = Convert.ToBase64String(
            HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signatureHeader));
    }
}
