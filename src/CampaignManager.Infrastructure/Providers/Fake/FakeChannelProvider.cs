using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Providers.Fake;

/// <summary>Base for dev/demo providers: logs the send, simulates latency, and fails a
/// configurable percentage of sends (settings key "failureRatePercent") so retry/failover
/// paths are exercisable without real provider accounts.</summary>
public abstract class FakeChannelProvider : IChannelProvider
{
    private readonly ILogger _logger;

    protected FakeChannelProvider(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Channel Channel { get; }
    public abstract string ProviderKey { get; }

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        await Task.Delay(Random.Shared.Next(5, 30), ct);

        var failureRate = 0;
        if (credentials.Settings.TryGetValue("failureRatePercent", out var rateRaw))
        {
            _ = int.TryParse(rateRaw, out failureRate);
        }

        if (Random.Shared.Next(100) < failureRate)
        {
            _logger.LogWarning("[{Provider}] simulated transient failure for {Address}",
                ProviderKey, request.RecipientAddress);
            return SendResult.TransientFailure("simulated_failure", "Simulated provider failure.");
        }

        var providerMessageId = $"fake-{Guid.NewGuid():N}";
        _logger.LogInformation("[{Provider}] sent to {Address}: \"{Body}\" (providerMessageId {ProviderMessageId})",
            ProviderKey, request.RecipientAddress, Truncate(request.Body), providerMessageId);
        return SendResult.Ok(providerMessageId);
    }

    private static string Truncate(string body) => body.Length <= 80 ? body : body[..77] + "...";
}

public sealed class FakeSmsProvider(ILogger<FakeSmsProvider> logger) : FakeChannelProvider(logger)
{
    public override Channel Channel => Channel.Sms;
    public override string ProviderKey => "fake-sms";
}

public sealed class FakeEmailProvider(ILogger<FakeEmailProvider> logger) : FakeChannelProvider(logger)
{
    public override Channel Channel => Channel.Email;
    public override string ProviderKey => "fake-email";
}

public sealed class FakeWhatsAppProvider(ILogger<FakeWhatsAppProvider> logger) : FakeChannelProvider(logger)
{
    public override Channel Channel => Channel.WhatsApp;
    public override string ProviderKey => "fake-whatsapp";
}
