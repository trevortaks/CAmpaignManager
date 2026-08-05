namespace CampaignManager.Application.Admin.Providers;

public sealed record ProviderSummary(
    Guid Id, string Channel, string ProviderKey, string Name, int Priority, bool IsEnabled,
    int? RateLimitPerMinute, int MaxRetries, int RetryDelaySeconds,
    DateTime? LastTestedAtUtc, bool? LastTestSucceeded, string? LastTestError);

public sealed record ProviderDetail(
    Guid Id, string Channel, string ProviderKey, string Name, int Priority, bool IsEnabled,
    int? RateLimitPerMinute, int MaxRetries, int RetryDelaySeconds,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<string> CredentialKeys,   // names only — values are never returned
    string? WebhookSecret);

public sealed class SaveProviderInput
{
    public Guid? Id { get; init; }
    public required string Channel { get; init; }
    public required string ProviderKey { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; } = 10;
    public bool IsEnabled { get; init; } = true;
    public int? RateLimitPerMinute { get; init; }
    public int MaxRetries { get; init; }
    public int RetryDelaySeconds { get; init; } = 2;
    /// <summary>Non-secret settings (from-number, host, …).</summary>
    public Dictionary<string, string> Settings { get; init; } = [];
    /// <summary>Secrets to set/replace. Empty dictionary = keep existing credentials.</summary>
    public Dictionary<string, string> Credentials { get; init; } = [];
    public string? WebhookSecret { get; init; }
}
