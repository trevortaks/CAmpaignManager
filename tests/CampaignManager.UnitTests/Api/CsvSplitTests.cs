using CampaignManager.Application.Imports;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.Api;

public class CsvSplitTests
{
    [Fact]
    public void Splits_plain_fields() =>
        CsvLine.Split("a,b,c").Should().Equal("a", "b", "c");

    [Fact]
    public void Handles_quoted_fields_with_commas() =>
        CsvLine.Split("\"Doe, Jane\",+263771234567")
            .Should().Equal("Doe, Jane", "+263771234567");

    [Fact]
    public void Handles_escaped_quotes() =>
        CsvLine.Split("\"say \"\"hi\"\"\",x")
            .Should().Equal("say \"hi\"", "x");

    [Fact]
    public void Empty_fields_are_preserved() =>
        CsvLine.Split("a,,c").Should().Equal("a", "", "c");
}
