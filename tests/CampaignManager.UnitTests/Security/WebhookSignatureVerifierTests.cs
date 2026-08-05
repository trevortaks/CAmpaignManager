using System.Security.Cryptography;
using System.Text;
using CampaignManager.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.Security;

public class WebhookSignatureVerifierTests
{
    private const string Secret = "app-secret";
    private const string Body = """{"providerMessageId":"wamid.123","status":"Delivered"}""";
    private const string Url = "https://api.example.com/api/webhooks/meta-whatsapp";

    [Fact]
    public void Meta_accepts_valid_hmac_sha256()
    {
        var signature = "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Body))).ToLowerInvariant();
        new MetaWebhookSignatureVerifier().Verify(Secret, Body, signature, Url).Should().BeTrue();
    }

    [Fact]
    public void Meta_rejects_wrong_secret_and_tampered_body()
    {
        var signature = "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Body))).ToLowerInvariant();
        var verifier = new MetaWebhookSignatureVerifier();
        verifier.Verify("other-secret", Body, signature, Url).Should().BeFalse();
        verifier.Verify(Secret, Body + " ", signature, Url).Should().BeFalse();
        verifier.Verify(Secret, Body, "sha1=abc", Url).Should().BeFalse();
    }

    [Fact]
    public void Twilio_accepts_valid_hmac_sha1_over_url_plus_body()
    {
        var signature = Convert.ToBase64String(
            HMACSHA1.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Url + Body)));
        new TwilioWebhookSignatureVerifier().Verify(Secret, Body, signature, Url).Should().BeTrue();
    }

    [Fact]
    public void Twilio_rejects_wrong_url()
    {
        var signature = Convert.ToBase64String(
            HMACSHA1.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Url + Body)));
        new TwilioWebhookSignatureVerifier()
            .Verify(Secret, Body, signature, "https://evil.example.com/hook").Should().BeFalse();
    }
}
