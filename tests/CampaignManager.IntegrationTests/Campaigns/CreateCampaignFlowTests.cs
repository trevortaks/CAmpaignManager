using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampaignManager.Contracts.Auth;
using CampaignManager.Contracts.Campaigns;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CampaignManager.IntegrationTests.Campaigns;

/// <summary>End-to-end API test against a real SQL Server (the podman dev container) using a
/// throwaway database per run so the dev database and Hangfire schema are untouched.</summary>
public sealed class CreateCampaignFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CreateCampaignFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_campaign_returns_202_and_is_queryable()
    {
        var client = _factory.CreateClient();

        var tokenResponse = await client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest { Email = "admin@demo.local", Password = "Admin!Passw0rd1" });
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest
        {
            Name = "Integration Test",
            Channel = "Sms",
            Sender = "ITEST",
            MessageBody = "Hello {{FirstName}}",
            Recipients =
            [
                new CampaignRecipientDto { Address = "+263771000001" },
                new CampaignRecipientDto { Address = "+263771000002" },
                new CampaignRecipientDto { Address = "+263771000001" } // duplicate — must be deduped
            ]
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!;
        created.CampaignId.Should().NotBeEmpty();
        created.TrackingId.Should().StartWith("CMP-");
        created.Status.Should().Be("Queued");

        var status = await client.GetFromJsonAsync<CampaignStatusResponse>(
            $"/api/campaigns/{created.CampaignId}");
        status!.Statistics.Total.Should().Be(2, "duplicate recipients are removed");
        status.Statistics.Queued.Should().Be(2);

        var byTracking = await client.GetFromJsonAsync<CampaignStatusResponse>(
            $"/api/campaigns/by-tracking/{created.TrackingId}");
        byTracking!.CampaignId.Should().Be(created.CampaignId);
    }

    [Fact]
    public async Task Invalid_campaign_returns_400_problem_details()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "cmk_dev_2f9c1a8e4b7d3f60");

        var response = await client.PostAsJsonAsync("/api/campaigns", new
        {
            name = "", channel = "Carrier-Pigeon", sender = "X", recipients = Array.Empty<object>()
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("errors");
    }

    [Fact]
    public async Task Campaigns_endpoint_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/campaigns");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"CampaignManager_Test_{Guid.NewGuid():N}";

    private string MasterConnectionString =>
        "Server=localhost,1433;Database=master;User Id=sa;Password=CampaignDev!Passw0rd;TrustServerCertificate=True";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    $"Server=localhost,1433;Database={_databaseName};User Id=sa;Password=CampaignDev!Passw0rd;TrustServerCertificate=True"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            using var connection = new SqlConnection(MasterConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort cleanup; a leftover test database is harmless.
        }
    }
}
