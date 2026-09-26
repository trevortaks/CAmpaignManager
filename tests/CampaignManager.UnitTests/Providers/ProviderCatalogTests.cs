using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using CampaignManager.Infrastructure.Providers.Registry;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CampaignManager.UnitTests.Providers;

public sealed class ProviderCatalogTests
{
    [Fact]
    public void Catalog_is_complete_and_channel_key_pairs_are_unique()
    {
        var expected = new[]
        {
            "fake-sms", "fake-email", "fake-whatsapp", "twilio", "africas-talking", "clickatell", "gikko",
            "smtp", "sendgrid", "mailgun", "ses", "meta-whatsapp", "twilio-whatsapp", "infobip"
        };

        ProviderCatalog.All.Select(p => p.Key).Should().BeEquivalentTo(expected);
        ProviderCatalog.All.Select(p => (p.Channel, p.Key)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_concrete_provider_implementation_has_a_catalog_entry()
    {
        var providerTypes = typeof(ProviderRegistry).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IChannelProvider).IsAssignableFrom(t));

        foreach (var type in providerTypes)
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(p => Substitute.For(new[] { p.ParameterType }, Array.Empty<object>())).ToArray();
            var provider = (IChannelProvider)constructor.Invoke(arguments);
            ProviderCatalog.Find(provider.Channel, provider.ProviderKey).Should().NotBeNull(type.Name);
        }
    }

    [Fact]
    public void Required_fields_and_field_types_are_validated()
    {
        var provider = ProviderCatalog.Find(Channel.WhatsApp, "infobip")!;

        var missing = ProviderCatalog.ValidateSettings(provider, new Dictionary<string, string>());
        var invalid = ProviderCatalog.ValidateSettings(provider, new Dictionary<string, string> { ["baseUrl"] = "not-a-url" });
        var credentials = ProviderCatalog.ValidateSecrets(provider, Array.Empty<string>());

        missing.Should().ContainKey("Settings[baseUrl]");
        invalid.Should().ContainKey("Settings[baseUrl]");
        credentials.Should().ContainKey("Credentials[apiKey]");
    }

    [Fact]
    public void Invalid_channel_provider_combination_is_rejected_by_catalog()
    {
        ProviderCatalog.Find(Channel.Email, "twilio").Should().BeNull();
    }
}
