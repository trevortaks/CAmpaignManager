using System.Globalization;
using System.Text;

namespace CampaignManager.Sdk.Internal;

/// <summary>Builds a URL query string from name/value pairs, skipping nulls and URL-encoding values.</summary>
internal sealed class QueryStringBuilder
{
    private readonly StringBuilder _builder = new();

    public QueryStringBuilder Add(string name, string? value)
    {
        if (value is null) return this;
        _builder.Append(_builder.Length == 0 ? '?' : '&');
        _builder.Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
        return this;
    }

    public QueryStringBuilder Add(string name, int? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryStringBuilder Add(string name, DateTime? value) =>
        Add(name, value?.ToString("O", CultureInfo.InvariantCulture));

    public override string ToString() => _builder.ToString();
}
