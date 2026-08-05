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
        var input = command.Input;

        if (!Enum.TryParse<Channel>(input.Channel, ignoreCase: true, out var channel))
        {
            throw new DomainException("Channel must be one of: Sms, Email, WhatsApp.");
        }

        if (!_registry.TryResolve(channel, input.ProviderKey, out _))
        {
            throw new DomainException(
                $"No provider implementation registered for {channel}/'{input.ProviderKey}'.");
        }

        ProviderConfiguration config;
        Channel? previousChannel = null;
        if (input.Id is { } id)
        {
            config = await _db.ProviderConfigurations.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new NotFoundException(nameof(ProviderConfiguration), id);
            previousChannel = config.Channel;
        }
        else
        {
            config = new ProviderConfiguration
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Channel = channel,
                ProviderKey = input.ProviderKey,
                Name = input.Name,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ProviderConfigurations.Add(config);
        }

        config.Channel = channel;
        config.ProviderKey = input.ProviderKey;
        config.Name = input.Name;
        config.Priority = input.Priority;
        config.IsEnabled = input.IsEnabled;
        config.RateLimitPerMinute = input.RateLimitPerMinute;
        config.MaxRetries = Math.Clamp(input.MaxRetries, 0, 5);
        config.RetryDelaySeconds = Math.Clamp(input.RetryDelaySeconds, 1, 300);
        config.SettingsJson = input.Settings.Count > 0 ? JsonSerializer.Serialize(input.Settings) : null;
        if (input.WebhookSecret is not null)
        {
            config.WebhookSecret = input.WebhookSecret;
        }

        if (input.Credentials.Count > 0)
        {
            config.EncryptedCredentials = _protector.Protect(input.Credentials);
        }

        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync(ProviderConfigCacheKeys.ForChannel(organizationId, channel), ct);
        if (previousChannel is { } prev && prev != channel)
        {
            await _cache.RemoveAsync(ProviderConfigCacheKeys.ForChannel(organizationId, prev), ct);
        }

        return config.Id;
    }
}

public sealed record SetProviderEnabledCommand(Guid ProviderId, bool Enabled) : IRequest;

public sealed class SetProviderEnabledHandler : IRequestHandler<SetProviderEnabledCommand>
{
    private readonly IAppDbContext _db;
    private readonly IDistributedCache _cache;

    public SetProviderEnabledHandler(IAppDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task Handle(SetProviderEnabledCommand command, CancellationToken ct)
    {
        var config = await _db.ProviderConfigurations
                .FirstOrDefaultAsync(p => p.Id == command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(ProviderConfiguration), command.ProviderId);
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
    private readonly Notifications.INotificationService _notifications;

    public TestProviderHandler(
        IAppDbContext db, IProviderRegistry registry, ICredentialProtector protector,
        Notifications.INotificationService notifications)
    {
        _db = db;
        _registry = registry;
        _protector = protector;
        _notifications = notifications;
    }

    public async Task<SendResult> Handle(TestProviderCommand command, CancellationToken ct)
    {
        var config = await _db.ProviderConfigurations
                .FirstOrDefaultAsync(p => p.Id == command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(ProviderConfiguration), command.ProviderId);

        SendResult result;
        if (!_registry.TryResolve(config.Channel, config.ProviderKey, out var provider) || provider is null)
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

        if (!result.Success)
        {
            var isAuthFailure = result.ErrorCode is "missing_credentials" or "401" or "403";
            await _notifications.NotifyAsync(
                config.OrganizationId,
                isAuthFailure
                    ? Notifications.NotificationEventTypes.ProviderAuthFailed
                    : Notifications.NotificationEventTypes.ProviderOffline,
                $"Provider test failed: {config.Name}",
                $"Connection test for provider '{config.Name}' ({config.ProviderKey}) failed: " +
                $"{result.ErrorCode}: {result.ErrorMessage}",
                ct);
        }

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
