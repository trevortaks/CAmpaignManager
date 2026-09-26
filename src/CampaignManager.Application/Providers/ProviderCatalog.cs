using CampaignManager.Domain.Enums;

namespace CampaignManager.Application.Providers;

public enum ProviderFieldType { Text, Secret, Url, Number, Boolean, Select }

public sealed record ProviderFieldDefinition(
    string Key, string Label, ProviderFieldType Type, bool Required = false,
    string? DefaultValue = null, string? HelpText = null, IReadOnlyList<string>? Options = null);

public sealed record ProviderDefinition(
    string Key, string DisplayName, Channel Channel,
    IReadOnlyList<ProviderFieldDefinition> Settings,
    IReadOnlyList<ProviderFieldDefinition> Secrets,
    bool SupportsConnectionTest,
    bool WebhookRelevant,
    bool IsDevelopment = false);

/// <summary>Static metadata for every built-in provider. This is intentionally a catalog, not a
/// plugin system: provider implementations remain ordinary DI registrations.</summary>
public static class ProviderCatalog
{
    private static ProviderFieldDefinition Text(string key, string label, bool required = false, string? help = null) =>
        new(key, label, ProviderFieldType.Text, required, HelpText: help);
    private static ProviderFieldDefinition Secret(string key, string label, bool required = true, string? help = null) =>
        new(key, label, ProviderFieldType.Secret, required, HelpText: help);
    private static ProviderFieldDefinition Url(string key, string label, bool required = false, string? help = null) =>
        new(key, label, ProviderFieldType.Url, required, HelpText: help);
    private static ProviderFieldDefinition Number(string key, string label, string defaultValue, string? help = null) =>
        new(key, label, ProviderFieldType.Number, DefaultValue: defaultValue, HelpText: help);
    private static ProviderFieldDefinition Boolean(string key, string label, string defaultValue, string? help = null) =>
        new(key, label, ProviderFieldType.Boolean, DefaultValue: defaultValue, HelpText: help);

    public static IReadOnlyList<ProviderDefinition> All { get; } =
    [
        new("fake-sms", "Fake SMS (development)", Channel.Sms,
            [Number("failureRatePercent", "Simulated failure rate (%)", "0", "0–100; useful for exercising failover.")], [], true, true, true),
        new("fake-email", "Fake Email (development)", Channel.Email,
            [Number("failureRatePercent", "Simulated failure rate (%)", "0", "0–100; useful for exercising failover.")], [], true, true, true),
        new("fake-whatsapp", "Fake WhatsApp (development)", Channel.WhatsApp,
            [Number("failureRatePercent", "Simulated failure rate (%)", "0", "0–100; useful for exercising failover.")], [], true, true, true),
        new("twilio", "Twilio SMS", Channel.Sms,
            [Text("fromNumber", "From number", help: "E.164 number; campaign sender is used when omitted.")],
            [Secret("accountSid", "Account SID"), Secret("authToken", "Auth token")], true, true),
        new("africas-talking", "Africa's Talking", Channel.Sms,
            [Text("username", "Username", true), Text("shortCode", "Short code / from number")],
            [Secret("apiKey", "API key")], true, false),
        new("clickatell", "Clickatell", Channel.Sms, [], [Secret("apiKey", "API key")], true, false),
        new("gikko", "Gikko SMS", Channel.Sms,
            [Url("baseUrl", "Gikko base URL", true, "Tenant URL supplied by Gikko."),
             Text("fromNumber", "Sender ID", help: "Campaign sender is used when omitted.")],
            [Secret("apiKey", "API key")], true, false),
        new("smtp", "SMTP", Channel.Email,
            [Text("host", "SMTP host", true), Number("port", "Port", "587"), Boolean("enableSsl", "Use TLS", "true")],
            [Secret("username", "Username", false), Secret("password", "Password", false)], false, false),
        new("sendgrid", "SendGrid", Channel.Email,
            [Text("fromName", "From name")], [Secret("apiKey", "API key")], true, false),
        new("mailgun", "Mailgun", Channel.Email,
            [Text("domain", "Sending domain", true, "For example mg.example.com.")], [Secret("apiKey", "API key")], true, false),
        new("ses", "Amazon SES", Channel.Email,
            [Text("region", "AWS region", help: "Defaults to us-east-1.") with { DefaultValue = "us-east-1" }],
            [Secret("accessKeyId", "Access key ID"), Secret("secretAccessKey", "Secret access key")], true, false),
        new("meta-whatsapp", "Meta WhatsApp Cloud API", Channel.WhatsApp,
            [Text("phoneNumberId", "Phone number ID", true)], [Secret("accessToken", "Access token")], true, true),
        new("twilio-whatsapp", "Twilio WhatsApp", Channel.WhatsApp,
            [Text("fromNumber", "WhatsApp from number", help: "E.164 without the whatsapp: prefix; campaign sender is used when omitted.")],
            [Secret("accountSid", "Account SID"), Secret("authToken", "Auth token")], true, false),
        new("infobip", "Infobip WhatsApp", Channel.WhatsApp,
            [Url("baseUrl", "Infobip base URL", true, "Tenant URL, for example https://xxxxx.api.infobip.com.")],
            [Secret("apiKey", "API key")], true, false)
    ];

    public static ProviderDefinition? Find(Channel channel, string key) =>
        All.FirstOrDefault(p => p.Channel == channel && string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyDictionary<string, string> ValidateSettings(
        ProviderDefinition provider, IReadOnlyDictionary<string, string> settings)
    {
        var errors = new Dictionary<string, string>();
        foreach (var field in provider.Settings)
        {
            settings.TryGetValue(field.Key, out var value);
            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                errors[$"Settings[{field.Key}]"] = $"{field.Label} is required.";
                continue;
            }
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (field.Type == ProviderFieldType.Url &&
                (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
                errors[$"Settings[{field.Key}]"] = $"{field.Label} must be an absolute HTTP or HTTPS URL.";
            else if (field.Type == ProviderFieldType.Number && !int.TryParse(value, out _))
                errors[$"Settings[{field.Key}]"] = $"{field.Label} must be a number.";
            else if (field.Type == ProviderFieldType.Boolean && !bool.TryParse(value, out _))
                errors[$"Settings[{field.Key}]"] = $"{field.Label} must be true or false.";
            else if (field.Type == ProviderFieldType.Select && field.Options is not null && !field.Options.Contains(value))
                errors[$"Settings[{field.Key}]"] = $"{field.Label} has an invalid value.";
        }
        return errors;
    }

    public static IReadOnlyDictionary<string, string> ValidateSecrets(
        ProviderDefinition provider, IEnumerable<string> availableKeys) =>
        provider.Secrets.Where(f => f.Required && !availableKeys.Contains(f.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(f => $"Credentials[{f.Key}]", f => $"{f.Label} is required.");

    public static IReadOnlyDictionary<string, string> ValidateSecrets(
        ProviderDefinition provider, IReadOnlyDictionary<string, string> secrets) =>
        provider.Secrets.Where(f => f.Required &&
            (!secrets.TryGetValue(f.Key, out var value) || string.IsNullOrWhiteSpace(value)))
            .ToDictionary(f => $"Credentials[{f.Key}]", f => $"{f.Label} is required.");

    public static Dictionary<string, string> ApplyDefaults(
        ProviderDefinition provider, IReadOnlyDictionary<string, string> settings)
    {
        var result = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        foreach (var field in provider.Settings.Where(f => f.DefaultValue is not null))
            result.TryAdd(field.Key, field.DefaultValue!);
        return result;
    }
}
