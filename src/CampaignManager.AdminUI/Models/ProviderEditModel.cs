using CampaignManager.Application.Admin.Providers;
using CampaignManager.Application.Providers;

namespace CampaignManager.AdminUI.Models;

public sealed class ProviderEditModel
{
    public Guid? Id { get; set; }
    public string Channel { get; set; } = "Sms";
    public string ProviderKey { get; set; } = "fake-sms";
    public string Name { get; set; } = "";
    public int Priority { get; set; } = 10;
    public int? RateLimitPerMinute { get; set; }
    public int MaxRetries { get; set; }
    public int RetryDelaySeconds { get; set; } = 2;
    public Dictionary<string, string> Settings { get; set; } = [];
    public Dictionary<string, string> Credentials { get; set; } = [];
    public SecretUpdateAction CredentialsAction { get; set; } = SecretUpdateAction.Replace;
    public string? WebhookSecret { get; set; }
    public SecretUpdateAction WebhookSecretAction { get; set; } = SecretUpdateAction.Keep;
    public IReadOnlyList<string> CredentialKeys { get; set; } = [];
    public bool HasWebhookSecret { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastTestedAtUtc { get; set; }
    public bool? LastTestSucceeded { get; set; }
    public IReadOnlyList<ProviderDefinition> Catalog { get; set; } = ProviderCatalog.All;
}
