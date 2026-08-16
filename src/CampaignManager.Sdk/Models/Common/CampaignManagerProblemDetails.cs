using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampaignManager.Sdk.Models.Common;

/// <summary>RFC 7807 problem shape returned by every non-2xx CampaignManager API response
/// (see GlobalExceptionHandler on the server). Validation failures carry a FluentValidation
/// "errors" extension: use <see cref="TryGetValidationErrors"/> to read it typed.
/// <para>ASP.NET Core's ProblemDetails serializer flattens extension members (like "errors")
/// into top-level JSON properties rather than nesting them under an "extensions" key, so
/// <see cref="Extensions"/> is populated via <see cref="JsonExtensionDataAttribute"/> — it
/// captures every property this type doesn't otherwise declare.</para></summary>
public sealed class CampaignManagerProblemDetails
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>Attempts to read the "errors" extension FluentValidation attaches to 400 responses
    /// (property name -&gt; messages). Returns false when the response carried no validation errors.</summary>
    public bool TryGetValidationErrors(out IReadOnlyDictionary<string, string[]>? errors)
    {
        errors = null;
        if (Extensions is null || !Extensions.TryGetValue("errors", out var element))
        {
            return false;
        }

        try
        {
            errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(element.GetRawText());
            return errors is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
