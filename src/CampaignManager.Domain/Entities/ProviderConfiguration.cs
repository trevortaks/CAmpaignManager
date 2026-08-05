using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.Entities;

public class ProviderConfiguration
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Channel Channel { get; set; }
    /// <summary>Matches IChannelProvider.ProviderKey, e.g. "fake-sms", "twilio", "smtp".</summary>
    public required string ProviderKey { get; set; }
    public required string Name { get; set; }
    /// <summary>Lower value = higher priority.</summary>
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    /// <summary>Data-Protection-encrypted JSON dictionary of secrets.</summary>
    public byte[]? EncryptedCredentials { get; set; }
    /// <summary>Non-secret settings (from-number, sender name, endpoint URL) as JSON.</summary>
    public string? SettingsJson { get; set; }
    /// <summary>Shared secret required on delivery webhooks for this configuration.</summary>
    public string? WebhookSecret { get; set; }
    /// <summary>Max messages per minute through this configuration; null = unlimited.</summary>
    public int? RateLimitPerMinute { get; set; }
    /// <summary>Immediate retries against this provider before failing over; 0 = none.</summary>
    public int MaxRetries { get; set; }
    /// <summary>Delay between immediate retries.</summary>
    public int RetryDelaySeconds { get; set; } = 2;
    public DateTime? LastTestedAtUtc { get; set; }
    public bool? LastTestSucceeded { get; set; }
    public string? LastTestError { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Organization? Organization { get; set; }
}
