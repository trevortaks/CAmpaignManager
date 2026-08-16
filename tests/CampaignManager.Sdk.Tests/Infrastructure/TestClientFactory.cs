using CampaignManager.Sdk;
using CampaignManager.Sdk.Auth;

namespace CampaignManager.Sdk.Tests.Infrastructure;

internal static class TestClientFactory
{
    public static CampaignManagerClient Create(StubHttpMessageHandler stub) =>
        new(new CampaignManagerClientOptions
        {
            BaseAddress = new Uri("https://campaigns.test/"),
            Credential = new ApiKeyCredential("cmk_test"),
            EnableRetries = false,
            PrimaryHandler = stub
        });

    public static HttpResponseMessage Json(System.Net.HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
