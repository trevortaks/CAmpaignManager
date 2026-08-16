using System.Text.Json;

namespace CampaignManager.Sdk.Internal;

/// <summary>The JSON options every request/response in this SDK serializes with. Must match
/// ASP.NET Core's unconfigured default (camelCase, case-insensitive on read) since the API doesn't
/// customize its JsonOptions — <see cref="JsonSerializerDefaults.Web"/> is exactly that.</summary>
public static class SdkJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
