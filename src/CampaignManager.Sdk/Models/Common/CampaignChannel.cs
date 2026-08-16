using System.Text.Json.Serialization;

namespace CampaignManager.Sdk.Models.Common;

/// <summary>Campaign delivery channel. JSON values exactly match the API wire format.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CampaignChannel>))]
public enum CampaignChannel
{
    Sms = 1,
    Email = 2,
    WhatsApp = 3
}
