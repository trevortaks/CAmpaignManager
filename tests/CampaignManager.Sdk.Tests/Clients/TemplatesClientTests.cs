using System.Net;
using CampaignManager.Sdk.Models.Templates;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CampaignManager.Sdk.Tests.Clients;

public sealed class TemplatesClientTests
{
    [Fact]
    public async Task CreateAsync_unwraps_templateId_envelope()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.Created,
            """{"templateId":"11111111-1111-1111-1111-111111111111"}"""));
        var client = TestClientFactory.Create(stub);

        var id = await client.Templates.CreateAsync(new SaveTemplateInput
        {
            Name = "Welcome", Channel = CampaignChannel.Email, Body = "Hi {{FirstName}}"
        });

        id.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    [Fact]
    public async Task PreviewAsync_posts_to_preview_route_and_returns_rendered_body()
    {
        var stub = new StubHttpMessageHandler(_ => TestClientFactory.Json(HttpStatusCode.OK,
            """{"subject":null,"body":"Hi Ada"}"""));
        var client = TestClientFactory.Create(stub);

        var preview = await client.Templates.PreviewAsync(new PreviewTemplateRequest(
            null, "Hi {{FirstName}}", null, new Dictionary<string, string> { ["FirstName"] = "Ada" }));

        preview.Body.Should().Be("Hi Ada");
        stub.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/templates/preview");
    }
}
