using CampaignManager.Domain.Enums;

namespace CampaignManager.Application.Providers;

public sealed record ProviderSendRequest(
    Guid MessagePublicId,
    string RecipientAddress,
    string Body,
    string? Subject,
    string Sender,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SendResult(
    bool Success,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsTransient)
{
    public static SendResult Ok(string providerMessageId) =>
        new(true, providerMessageId, null, null, false);

    /// <summary>Transient failure (connectivity, auth, provider 5xx) — eligible for failover/retry.</summary>
    public static SendResult TransientFailure(string code, string message) =>
        new(false, null, code, message, true);

    /// <summary>Permanent recipient-level rejection (invalid address) — no failover, no retry.</summary>
    public static SendResult Rejected(string code, string message) =>
        new(false, null, code, message, false);
}

/// <summary>Decrypted credentials plus non-secret settings for one ProviderConfiguration.</summary>
public sealed record ProviderCredentials(
    IReadOnlyDictionary<string, string> Secrets,
    IReadOnlyDictionary<string, string> Settings);

public interface IChannelProvider
{
    Channel Channel { get; }
    /// <summary>Stable key matching ProviderConfiguration.ProviderKey, e.g. "fake-sms", "twilio".</summary>
    string ProviderKey { get; }
    Task<SendResult> SendAsync(ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct);
}
