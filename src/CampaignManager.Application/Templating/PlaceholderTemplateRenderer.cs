using System.Text.RegularExpressions;

namespace CampaignManager.Application.Templating;

public sealed partial class PlaceholderTemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    public string Render(string template, IReadOnlyDictionary<string, string>? values)
    {
        if (string.IsNullOrEmpty(template)) return template;

        // Single pass — substituted values are never re-expanded, so a value
        // containing "{{...}}" cannot inject further tokens.
        return TokenRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (values is null) return string.Empty;
            foreach (var (candidateKey, value) in values)
            {
                if (string.Equals(candidateKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }

            return string.Empty;
        });
    }
}
