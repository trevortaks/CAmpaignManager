using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CampaignManager.Application.Admin.Providers;

public sealed record SaveProviderCommand(SaveProviderInput Input) : IRequest<Guid>;

public sealed class SaveProviderHandler : IRequestHandler<SaveProviderCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ICredentialProtector _protector;
    private readonly IProviderRegistry _registry;
    private readonly IDistributedCache _cache;

    public SaveProviderHandler(
        IAppDbContext db, ICurrentTenant tenant, ICredentialProtector protector, IProviderRegistry registry,
        IDistributedCache cache)
    {
        _db = db;
        _tenant = tenant;
        _protector = protector;
        _registry = registry;
        _cache = cache;
    }

    public async Task<Guid> Handle(SaveProviderCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId
            ?? throw new DomainException("No organization context.");
        if (!await _db.Organizations.AnyAsync(o => o.Id == organizationId, ct))
        {
            throw new DomainException(
                "Your signed-in organization no longer exists. Restart the API to repair the development seed, then sign out and sign in again.");
        }
        var input = command.Input;

        if (!Enum.TryParse<Channel>(input.Channel, ignoreCase: true, out var channel))
        {
            throw new DomainException("Channel must be one of: Sms, Email, WhatsApp.");
        }

        var definition = ProviderCatalog.Find(channel, input.ProviderKey)
            ?? throw new DomainException($"'{input.ProviderKey}' is not available for {channel}.");

        if (!_registry.TryResolve(channel, definition.Key, out _))
        {
            throw new DomainException(
                $"No provider implementation registered for {channel}/'{input.ProviderKey}'.");
        }

        ProviderConfiguration config;
        Channel? previousChannel = null;
        Dictionary<string, string> previousSettings = [];
        Dictionary<string, string> previousCredentials = [];
        if (input.Id is { } id)
        {
            config = await _db.ProviderConfigurations.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new NotFoundException(nameof(ProviderConfiguration), id);
            previousChannel = config.Channel;
            previousSettings = string.IsNullOrEmpty(config.SettingsJson)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, string>>(config.SettingsJson) ?? [];
            previousCredentials = config.EncryptedCredentials is { Length: > 0 }
                ? new Dictionary<string, string>(_protector.Unprotect(config.EncryptedCredentials))
                : [];
        }
        else
        {
            config = new ProviderConfiguration
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Channel = channel,
                ProviderKey = definition.Key,
                Name = input.Name,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ProviderConfigurations.Add(config);
        }

        if (string.IsNullOrWhiteSpace(input.Name)) throw new DomainException("Provider name is required.");
        if (input.Priority < 1) throw new DomainException("Failover position must be at least 1.");
        if (input.RateLimitPerMinute is <= 0) throw new DomainException("Rate limit must be positive when set.");
        if (input.MaxRetries is < 0 or > 5) throw new DomainException("Max retries must be between 0 and 5.");
        if (input.RetryDelaySeconds is < 1 or > 300) throw new DomainException("Retry delay must be between 1 and 300 seconds.");

        var settings = ProviderCatalog.ApplyDefaults(definition, input.Settings);
        var settingErrors = ProviderCatalog.ValidateSettings(definition, settings);
        if (settingErrors.Count > 0) throw new DomainException(string.Join(" ", settingErrors.Values));

        var credentials = input.CredentialsAction switch
        {
            SecretUpdateAction.Keep => previousCredentials,
            SecretUpdateAction.Replace => input.Credentials,
            SecretUpdateAction.Clear => [],
            _ => throw new DomainException("Invalid credential update action.")
        };
        if (input.CredentialsAction != SecretUpdateAction.Clear)
        {
            var credentialErrors = ProviderCatalog.ValidateSecrets(definition, credentials);
            if (credentialErrors.Count > 0) throw new DomainException(string.Join(" ", credentialErrors.Values));
        }

        var nextWebhookSecret = input.WebhookSecretAction switch
        {
            SecretUpdateAction.Keep => config.WebhookSecret,
            SecretUpdateAction.Replace when !string.IsNullOrWhiteSpace(input.WebhookSecret) => input.WebhookSecret,
            SecretUpdateAction.Replace => throw new DomainException("A webhook secret is required when replacing it."),
            SecretUpdateAction.Clear => null,
            _ => throw new DomainException("Invalid webhook secret update action.")
        };

        var maxRetries = input.MaxRetries;
        var retryDelaySeconds = input.RetryDelaySeconds;
        var connectionChanged = input.Id is null || config.Channel != channel ||
            !string.Equals(config.ProviderKey, definition.Key, StringComparison.OrdinalIgnoreCase) ||
            !DictionariesEqual(previousSettings, settings) || !DictionariesEqual(previousCredentials, credentials) ||
            !string.Equals(config.WebhookSecret, nextWebhookSecret, StringComparison.Ordinal) ||
            config.Priority != input.Priority || config.RateLimitPerMinute != input.RateLimitPerMinute ||
            config.MaxRetries != maxRetries || config.RetryDelaySeconds != retryDelaySeconds;

        config.Channel = channel;
        config.ProviderKey = definition.Key;
        config.Name = input.Name;
        config.Priority = input.Priority;
        if (input.Id is null || connectionChanged) config.IsEnabled = false;
        config.RateLimitPerMinute = input.RateLimitPerMinute;
        config.MaxRetries = maxRetries;
        config.RetryDelaySeconds = retryDelaySeconds;
        config.SettingsJson = settings.Count > 0 ? JsonSerializer.Serialize(settings) : null;
        config.WebhookSecret = nextWebhookSecret;
        config.EncryptedCredentials = credentials.Count > 0 ? _protector.Protect(credentials) : null;
        if (connectionChanged)
        {
            config.LastTestedAtUtc = null;
            config.LastTestSucceeded = null;
            config.LastTestError = null;
        }

        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync(ProviderConfigCacheKeys.ForChannel(organizationId, channel), ct);
        if (previousChannel is { } prev && prev != channel)
        {
            await _cache.RemoveAsync(ProviderConfigCacheKeys.ForChannel(organizationId, prev), ct);
        }

        return config.Id;
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair =>
            right.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));
}

public sealed record SetProviderEnabledCommand(Guid ProviderId, bool Enabled, bool ConfirmUntested = false) : IRequest;

public sealed class SetProviderEnabledHandler : IRequestHandler<SetProviderEnabledCommand>
{
    private readonly IAppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ICredentialProtector _protector;

    public SetProviderEnabledHandler(IAppDbContext db, IDistributedCache cache, ICredentialProtector protector)
    {
        _db = db;
        _cache = cache;
        _protector = protector;
    }

    public async Task Handle(SetProviderEnabledCommand command, CancellationToken ct)
    {
        var config = await _db.ProviderConfigurations
                .FirstOrDefaultAsync(p => p.Id == command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(ProviderConfiguration), command.ProviderId);
        if (command.Enabled)
        {
            var definition = ProviderCatalog.Find(config.Channel, config.ProviderKey)
                ?? throw new DomainException("This provider/channel combination is not supported.");
            var settings = string.IsNullOrEmpty(config.SettingsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(config.SettingsJson) ?? [];
            var credentials = config.EncryptedCredentials is { Length: > 0 }
                ? _protector.Unprotect(config.EncryptedCredentials)
                : new Dictionary<string, string>();
            var errors = ProviderCatalog.ValidateSettings(definition, settings)
                .Concat(ProviderCatalog.ValidateSecrets(definition, credentials)).ToList();
            if (errors.Count > 0) throw new DomainException(string.Join(" ", errors.Select(e => e.Value)));
            if (definition.SupportsConnectionTest && config.LastTestSucceeded != true)
                throw new DomainException("Test the connection successfully before enabling this provider.");
            if (!definition.SupportsConnectionTest && !command.ConfirmUntested)
                throw new DomainException("This provider has no safe connection test. Confirm that you want to enable it untested.");
        }

        config.IsEnabled = command.Enabled;
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProviderConfigCacheKeys.ForChannel(config.OrganizationId, config.Channel), ct);
    }
}

public sealed record TestProviderCommand(Guid ProviderId) : IRequest<SendResult>;

public sealed class TestProviderHandler : IRequestHandler<TestProviderCommand, SendResult>
{
    private readonly IAppDbContext _db;
    private readonly IProviderRegistry _registry;
    private readonly ICredentialProtector _protector;

    public TestProviderHandler(
        IAppDbContext db, IProviderRegistry registry, ICredentialProtector protector)
    {
        _db = db;
        _registry = registry;
        _protector = protector;
    }

    public async Task<SendResult> Handle(TestProviderCommand command, CancellationToken ct)
    {
        var config = await _db.ProviderConfigurations
                .FirstOrDefaultAsync(p => p.Id == command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(ProviderConfiguration), command.ProviderId);

        SendResult result;
        var definition = ProviderCatalog.Find(config.Channel, config.ProviderKey);
        if (definition is null)
        {
            result = SendResult.TransientFailure("invalid_configuration", "This provider/channel combination is not supported.");
        }
        else if (!definition.SupportsConnectionTest)
        {
            result = SendResult.TransientFailure("not_testable", "This provider does not support a safe connection test.");
        }
        else if (!_registry.TryResolve(config.Channel, config.ProviderKey, out var provider) || provider is null)
        {
            result = SendResult.TransientFailure("unregistered",
                $"No implementation registered for '{config.ProviderKey}'.");
        }
        else if (provider is not ITestableProvider testable)
        {
            result = SendResult.TransientFailure("not_testable",
                "This provider does not support connection testing.");
        }
        else
        {
            var secrets = config.EncryptedCredentials is { Length: > 0 }
                ? _protector.Unprotect(config.EncryptedCredentials)
                : new Dictionary<string, string>();
            var settings = string.IsNullOrEmpty(config.SettingsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(config.SettingsJson) ?? [];
            try
            {
                result = await testable.TestAsync(new ProviderCredentials(secrets, settings), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = SendResult.TransientFailure("test_exception", ex.Message);
            }
        }

        config.LastTestedAtUtc = DateTime.UtcNow;
        config.LastTestSucceeded = result.Success;
        config.LastTestError = result.Success ? null : $"{result.ErrorCode}: {result.ErrorMessage}";
        await _db.SaveChangesAsync(ct);

        return result;
    }
}

public sealed record DeleteProviderCommand(Guid ProviderId) : IRequest;

public sealed class DeleteProviderHandler : IRequestHandler<DeleteProviderCommand>
{
    private readonly IAppDbContext _db;
    private readonly IDistributedCache _cache;

    public DeleteProviderHandler(IAppDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task Handle(DeleteProviderCommand command, CancellationToken ct)
    {
        var config = await _db.ProviderConfigurations
                .FirstOrDefaultAsync(p => p.Id == command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(ProviderConfiguration), command.ProviderId);

        var used = await _db.Messages.IgnoreQueryFilters()
            .AnyAsync(m => m.ProviderConfigurationId == config.Id, ct);
        if (used)
        {
            // Preserve reporting history; disable instead of deleting.
            throw new DomainException(
                "This provider has sent messages and cannot be deleted; disable it instead.");
        }

        _db.ProviderConfigurations.Remove(config);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProviderConfigCacheKeys.ForChannel(config.OrganizationId, config.Channel), ct);
    }
}
