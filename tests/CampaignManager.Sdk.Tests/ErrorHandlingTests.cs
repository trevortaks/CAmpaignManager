using System.Text.Json;
using CampaignManager.Sdk.Exceptions;
using CampaignManager.Sdk.Models.Common;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests;

public sealed class ErrorHandlingTests
{
    [Fact]
    public void TryGetValidationErrors_deserializes_the_FluentValidation_errors_extension()
    {
        var json = """
            {"title":"Validation failed","status":400,"detail":"boom",
             "errors":{"Name":["'Name' must not be empty."],"Recipients":["At least one recipient is required."]}}
            """;
        var problem = JsonSerializer.Deserialize<CampaignManagerProblemDetails>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        problem.TryGetValidationErrors(out var errors).Should().BeTrue();
        errors!["Name"].Should().ContainSingle().Which.Should().Contain("must not be empty");
        errors["Recipients"].Should().ContainSingle();
    }

    [Fact]
    public void Message_combines_title_status_and_detail()
    {
        var problem = new CampaignManagerProblemDetails { Title = "Resource not found", Detail = "Campaign abc not found" };
        var exception = new CampaignManagerApiException(404, problem);

        exception.Message.Should().Be("Resource not found (404): Campaign abc not found");
        exception.StatusCode.Should().Be(404);
    }

    [Fact]
    public void Message_falls_back_to_a_generic_title_when_no_problem_body_was_returned()
    {
        var exception = new CampaignManagerApiException(429, null);

        exception.Message.Should().Be("Request failed (429)");
        exception.ProblemDetails.Should().BeNull();
    }
}
