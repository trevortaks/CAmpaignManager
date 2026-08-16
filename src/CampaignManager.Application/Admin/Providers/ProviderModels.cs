namespace CampaignManager.Application.Admin.Providers;

public sealed record ProviderSummary(
    Guid Id, string Channel, string ProviderKey, string Name, int Priority, bool IsEnabled,
    int? RateLimitPerMinute, int MaxRetries, int RetryDelaySeconds,
    DateTime? LastTestedAtUtc, bool? LastTestSucceeded, string? LastTestError,
    bool SupportsConnectionTest);

public sealed record ProviderDetail(
    Guid Id, string Channel, string ProviderKey, string Name, int Priority, bool IsEnabled,
    int? RateLimitPerMinute, int MaxRetries, int RetryDelaySeconds,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<string> CredentialKeys,   // names only — values are never returned
    bool HasWebhookSecret,
    DateTime? LastTestedAtUtc, bool? LastTestSucceeded);

public enum SecretUpdateAction { Keep, Replace, Clear }

public sealed class SaveProviderInput
{
    public Guid? Id { get; init; }
    public required string Channel { get; init; }
    public required string ProviderKey { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; } = 10;
    public int? RateLimitPerMinute { get; init; }
    public int MaxRetries { get; init; }
    public int RetryDelaySeconds { get; init; } = 2;
    /// <summary>Non-secret settings (from-number, host, …).</summary>
    public Dictionary<string, string> Settings { get; init; } = [];
    /// <summary>Secret values are accepted only when <see cref="CredentialsAction"/> is Replace.</summary>
    public Dictionary<string, string> Credentials { get; init; } = [];
    public SecretUpdateAction CredentialsAction { get; init; } = SecretUpdateAction.Keep;
    public string? WebhookSecret { get; init; }
    public SecretUpdateAction WebhookSecretAction { get; init; } = SecretUpdateAction.Keep;
}
