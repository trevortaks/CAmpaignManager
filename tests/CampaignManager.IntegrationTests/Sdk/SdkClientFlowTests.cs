using CampaignManager.IntegrationTests.Campaigns;
using CampaignManager.Sdk;
using CampaignManager.Sdk.Auth;
using CampaignManager.Sdk.Models.Campaigns;
using CampaignManager.Sdk.Models.Common;
using CampaignManager.Sdk.Models.Series;
using CampaignManager.Sdk.Models.Templates;
using FluentAssertions;
using Xunit;

namespace CampaignManager.IntegrationTests.Sdk;

/// <summary>Runs CampaignManager.Sdk against a real in-memory API instance (ApiFactory), so drift
/// between the SDK's hand-mirrored DTOs and the actual wire shapes is caught by CI rather than by
/// manual inspection. Requires SQL Server (podman-compose up -d) — see ApiFactory.</summary>
[Collection("ApiFactory")]
public sealed class SdkClientFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SdkClientFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private CampaignManagerClient CreateSdkClient()
    {
        // ApiFactory's seeded dev API key (see CreateCampaignFlowTests) — exercises the
        // ApiKeyCredential path end-to-end. Server.CreateHandler() gives an HttpMessageHandler
        // wired directly into the in-memory TestServer, which is what PrimaryHandler exists for.
        _ = _factory.Server; // force host startup before reading BaseAddress/CreateHandler
        return new CampaignManagerClient(new CampaignManagerClientOptions
        {
            BaseAddress = _factory.Server.BaseAddress,
            Credential = new ApiKeyCredential("cmk_dev_2f9c1a8e4b7d3f60"),
            EnableRetries = false,
            PrimaryHandler = _factory.Server.CreateHandler()
        });
    }

    [Fact]
    public async Task Create_then_get_campaign_round_trips_through_the_sdk()
    {
        var client = CreateSdkClient();
        var address = $"+2637720{Random.Shared.Next(10000, 99999)}";

        var created = await client.Campaigns.CreateAsync(new CreateCampaignRequest
        {
            Name = "SDK flow test",
            Channel = CampaignChannel.Sms,
            Sender = "SDKTEST",
            MessageBody = "hi",
            Recipients = [new CampaignRecipientDto { Address = address }]
        });

        created.CampaignId.Should().NotBeEmpty();

        var status = await client.Campaigns.GetAsync(created.CampaignId);
        status.CampaignId.Should().Be(created.CampaignId);
        status.Statistics.Total.Should().Be(1);

        var byTracking = await client.Campaigns.GetByTrackingIdAsync(created.TrackingId);
        byTracking.CampaignId.Should().Be(created.CampaignId);
    }

    [Fact]
    public async Task Series_create_pause_resume_delete_round_trips_through_the_sdk()
    {
        var client = CreateSdkClient();

        var seriesId = await client.CampaignSeries.CreateAsync(new SaveCampaignSeriesInput
        {
            Name = "SDK series test",
            Channel = CampaignChannel.Sms,
            Sender = "SDKTEST",
            MessageBody = "hi {{FirstName}}",
            CronExpression = "0 9 * * MON",
            Recipients = [new SeriesRecipientInput("+263772000001", null)]
        });

        await client.CampaignSeries.PauseAsync(seriesId);
        var paused = await client.CampaignSeries.GetAsync(seriesId);
        paused.IsActive.Should().BeFalse();

        await client.CampaignSeries.ResumeAsync(seriesId);
        var resumed = await client.CampaignSeries.GetAsync(seriesId);
        resumed.IsActive.Should().BeTrue();

        await client.CampaignSeries.DeleteAsync(seriesId);
        var list = await client.CampaignSeries.ListAsync();
        list.Should().NotContain(s => s.Id == seriesId);
    }

    [Fact]
    public async Task Template_create_preview_delete_round_trips_through_the_sdk()
    {
        var client = CreateSdkClient();

        var templateId = await client.Templates.CreateAsync(new SaveTemplateInput
        {
            Name = "SDK template test",
            Channel = CampaignChannel.Email,
            Subject = "Hello {{FirstName}}",
            Body = "Hi {{FirstName}}, welcome!"
        });

        var preview = await client.Templates.PreviewAsync(new PreviewTemplateRequest(
            templateId, null, null, new Dictionary<string, string> { ["FirstName"] = "Ada" }));
        preview.Body.Should().Be("Hi Ada, welcome!");

        await client.Templates.DeleteAsync(templateId);
        var list = await client.Templates.ListAsync();
        list.Should().NotContain(t => t.Id == templateId && t.IsActive);
    }

    [Fact]
    public async Task Suppress_list_erase_round_trips_through_the_sdk()
    {
        var client = CreateSdkClient();
        var address = $"opt-out-{Guid.NewGuid():N}@example.com";

        var suppressionId = await client.Compliance.SuppressAsync(address, reason: "unsubscribe");
        suppressionId.Should().NotBeEmpty();

        var list = await client.Compliance.ListSuppressionsAsync();
        list.Should().Contain(s => s.Id == suppressionId);

        var rowsRedacted = await client.Compliance.EraseAsync(address);
        rowsRedacted.Should().BeGreaterThanOrEqualTo(0);

        await client.Compliance.RemoveSuppressionAsync(suppressionId);
    }
}
