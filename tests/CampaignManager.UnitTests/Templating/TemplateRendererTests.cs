using CampaignManager.Application.Templating;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.Templating;

public class TemplateRendererTests
{
    private readonly PlaceholderTemplateRenderer _renderer = new();

    [Fact]
    public void Replaces_known_placeholders()
    {
        var result = _renderer.Render(
            "Hello {{FirstName}}, your appointment is on {{Date}}.",
            new Dictionary<string, string> { ["FirstName"] = "Ada", ["Date"] = "Friday" });
        result.Should().Be("Hello Ada, your appointment is on Friday.");
    }

    [Fact]
    public void Keys_are_case_insensitive()
    {
        _renderer.Render("Hi {{firstname}}", new Dictionary<string, string> { ["FirstName"] = "Ada" })
            .Should().Be("Hi Ada");
    }

    [Fact]
    public void Unknown_placeholders_become_empty()
    {
        _renderer.Render("Hi {{Missing}}!", new Dictionary<string, string>())
            .Should().Be("Hi !");
    }

    [Fact]
    public void Tolerates_whitespace_inside_braces()
    {
        _renderer.Render("Hi {{ FirstName }}", new Dictionary<string, string> { ["FirstName"] = "Ada" })
            .Should().Be("Hi Ada");
    }

    [Fact]
    public void Values_containing_tokens_are_not_recursively_expanded()
    {
        var result = _renderer.Render(
            "Hi {{A}}",
            new Dictionary<string, string> { ["A"] = "{{B}}", ["B"] = "evil" });
        result.Should().Be("Hi {{B}}");
    }

    [Fact]
    public void Null_values_dictionary_blanks_all_tokens()
    {
        _renderer.Render("Hi {{Name}}", null).Should().Be("Hi ");
    }
}
