using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Admin.Providers;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using CampaignManager.Infrastructure.Persistence;
using CampaignManager.Infrastructure.Providers.Registry;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace CampaignManager.UnitTests.Providers;

public sealed class ProviderAdministrationTests
{
    [Theory]
    [InlineData(SecretUpdateAction.Keep, "old")]
    [InlineData(SecretUpdateAction.Replace, "new")]
    [InlineData(SecretUpdateAction.Clear, null)]
    public async Task Credentials_support_keep_replace_and_clear(SecretUpdateAction action, string? expected)
    {
        var setup = CreateSetup();
        var config = ExistingConfig(setup.OrganizationId);
        setup.Db.ProviderConfigurations.Add(config);
        await setup.Db.SaveChangesAsync();

        await setup.Save.Handle(new SaveProviderCommand(new SaveProviderInput
        {
            Id = config.Id, Channel = "Sms", ProviderKey = "twilio", Name = "Twilio",
            Settings = [], CredentialsAction = action,
            Credentials = action == SecretUpdateAction.Replace
                ? new() { ["accountSid"] = "new", ["authToken"] = "new-token" }
                : []
        }), CancellationToken.None);

        var saved = await setup.Db.ProviderConfigurations.FindAsync(config.Id);
        if (expected is null) saved!.EncryptedCredentials.Should().BeNull();
        else setup.Protector.Unprotect(saved!.EncryptedCredentials!)["accountSid"].Should().Be(expected);
    }

    [Theory]
    [InlineData(SecretUpdateAction.Keep, "existing")]
    [InlineData(SecretUpdateAction.Replace, "replacement")]
    [InlineData(SecretUpdateAction.Clear, null)]
    public async Task Webhook_secret_supports_keep_replace_and_clear(SecretUpdateAction action, string? expected)
    {
        var setup = CreateSetup();
        var config = ExistingConfig(setup.OrganizationId);
        setup.Db.ProviderConfigurations.Add(config);
        await setup.Db.SaveChangesAsync();

        await setup.Save.Handle(new SaveProviderCommand(new SaveProviderInput
        {
            Id = config.Id, Channel = "Sms", ProviderKey = "twilio", Name = "Twilio",
            CredentialsAction = SecretUpdateAction.Keep,
            WebhookSecretAction = action,
            WebhookSecret = action == SecretUpdateAction.Replace ? "replacement" : null
        }), CancellationToken.None);

        (await setup.Db.ProviderConfigurations.FindAsync(config.Id))!.WebhookSecret.Should().Be(expected);
    }

    [Fact]
    public async Task Connection_change_disables_provider_and_invalidates_successful_test()
    {
        var setup = CreateSetup();
        var config = ExistingConfig(setup.OrganizationId);
        setup.Db.ProviderConfigurations.Add(config);
        await setup.Db.SaveChangesAsync();

        await setup.Save.Handle(new SaveProviderCommand(new SaveProviderInput
        {
            Id = config.Id, Channel = "Sms", ProviderKey = "twilio", Name = "Twilio",
            Settings = new() { ["fromNumber"] = "+2632" }, CredentialsAction = SecretUpdateAction.Keep
        }), CancellationToken.None);

        var saved = await setup.Db.ProviderConfigurations.FindAsync(config.Id);
        saved!.IsEnabled.Should().BeFalse();
        saved.LastTestSucceeded.Should().BeNull();
        saved.LastTestedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Testable_provider_requires_successful_test_before_enablement()
    {
        var setup = CreateSetup();
        var config = ExistingConfig(setup.OrganizationId);
        config.LastTestSucceeded = false;
        setup.Db.ProviderConfigurations.Add(config);
        await setup.Db.SaveChangesAsync();
        var handler = new SetProviderEnabledHandler(setup.Db, setup.Cache, setup.Protector);

        var act = () => handler.Handle(new SetProviderEnabledCommand(config.Id, true), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Test the connection*");
    }

    [Fact]
    public async Task Provider_without_safe_test_requires_explicit_enablement_confirmation()
    {
        var setup = CreateSetup();
        var config = new ProviderConfiguration
        {
            Id = Guid.NewGuid(), OrganizationId = setup.OrganizationId, Channel = Channel.Email,
            ProviderKey = "smtp", Name = "SMTP", IsEnabled = false,
            SettingsJson = "{\"host\":\"smtp.example.com\",\"port\":\"587\",\"enableSsl\":\"true\"}",
            CreatedAtUtc = DateTime.UtcNow
        };
        setup.Db.ProviderConfigurations.Add(config);
        await setup.Db.SaveChangesAsync();
        var handler = new SetProviderEnabledHandler(setup.Db, setup.Cache, setup.Protector);

        await FluentActions.Awaiting(() => handler.Handle(
                new SetProviderEnabledCommand(config.Id, true), CancellationToken.None))
            .Should().ThrowAsync<DomainException>().WithMessage("*Confirm*");

        await handler.Handle(new SetProviderEnabledCommand(config.Id, true, ConfirmUntested: true), CancellationToken.None);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Manual_connection_test_records_failure_without_a_notification_dependency()
    {
        var setup = CreateSetup(testResult: SendResult.TransientFailure("401", "bad credentials"));
        var config = ExistingConfig(setup.OrganizationId);
        setup.Db.ProviderConfigurations.Add(config);
        await setup.Db.SaveChangesAsync();
        var handler = new TestProviderHandler(setup.Db, setup.Registry, setup.Protector);

        var result = await handler.Handle(new TestProviderCommand(config.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        (await setup.Db.ProviderConfigurations.FindAsync(config.Id))!.LastTestSucceeded.Should().BeFalse();
    }

    private static Setup CreateSetup(SendResult? testResult = null)
    {
        var organizationId = Guid.NewGuid();
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.OrganizationId.Returns(organizationId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, tenant);
        var provider = new TestProvider(testResult ?? SendResult.Ok("test-ok"));
        var registry = new ProviderRegistry([provider]);
        var protector = new PlainProtector();
        var cache = Substitute.For<IDistributedCache>();
        var save = new SaveProviderHandler(db, tenant, protector, registry, cache);
        return new Setup(organizationId, db, registry, protector, cache, save);
    }

    private static ProviderConfiguration ExistingConfig(Guid organizationId) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, Channel = Channel.Sms,
        ProviderKey = "twilio", Name = "Twilio", IsEnabled = true,
        SettingsJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["fromNumber"] = "+2631" }),
        EncryptedCredentials = new PlainProtector().Protect(new Dictionary<string, string>
        {
            ["accountSid"] = "old", ["authToken"] = "old-token"
        }),
        WebhookSecret = "existing", LastTestSucceeded = true, LastTestedAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow
    };

    private sealed record Setup(Guid OrganizationId, AppDbContext Db, ProviderRegistry Registry,
        PlainProtector Protector, IDistributedCache Cache, SaveProviderHandler Save);

    private sealed class PlainProtector : ICredentialProtector
    {
        public byte[] Protect(IReadOnlyDictionary<string, string> credentials) => JsonSerializer.SerializeToUtf8Bytes(credentials);
        public IReadOnlyDictionary<string, string> Unprotect(byte[] encrypted) =>
            JsonSerializer.Deserialize<Dictionary<string, string>>(encrypted)!;
    }

    private sealed class TestProvider(SendResult testResult) : IChannelProvider, ITestableProvider
    {
        public Channel Channel => Channel.Sms;
        public string ProviderKey => "twilio";
        public Task<SendResult> SendAsync(ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct) =>
            Task.FromResult(SendResult.Ok("sent"));
        public Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct) => Task.FromResult(testResult);
    }
}
