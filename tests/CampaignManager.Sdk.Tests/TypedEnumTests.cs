using System.Text.Json;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Models.Common;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests;

public sealed class TypedEnumTests
{
    [Theory]
    [InlineData(CampaignChannel.Sms, "Sms")]
    [InlineData(CampaignChannel.Email, "Email")]
    [InlineData(CampaignChannel.WhatsApp, "WhatsApp")]
    public void Channel_serializes_with_exact_server_value(CampaignChannel channel, string expected)
    {
        var json = JsonSerializer.Serialize(new CreateCampaignRequest
        {
            Name = "x", Channel = channel, Sender = "x", MessageBody = "x",
            Recipients = [new CampaignRecipientDto { Address = "x" }]
        });

        json.Should().Contain($"\"Channel\":\"{expected}\"");
    }
}
