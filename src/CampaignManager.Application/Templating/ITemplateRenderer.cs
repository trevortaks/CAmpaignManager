namespace CampaignManager.Application.Templating;

public interface ITemplateRenderer
{
    /// <summary>Replaces {{Placeholder}} tokens with values (case-insensitive keys).
    /// Unmatched tokens are replaced with an empty string.</summary>
    string Render(string template, IReadOnlyDictionary<string, string>? values);
}
