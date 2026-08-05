using System.Diagnostics.Metrics;

namespace CampaignManager.Application.Observability;

/// <summary>Custom application metrics, instrumented directly in the send pipeline and
/// campaign lifecycle. A single static Meter is shared across the process (Api or Workers);
/// exported via OpenTelemetry (console/OTLP/Prometheus depending on host configuration).</summary>
public static class CampaignMetrics
{
    public const string MeterName = "CampaignManager";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> MessagesSent =
        Meter.CreateCounter<long>("campaignmanager.messages.sent", "messages", "Messages successfully sent");

    public static readonly Counter<long> MessagesFailed =
        Meter.CreateCounter<long>("campaignmanager.messages.failed", "messages", "Messages that failed or were rejected");

    public static readonly Counter<long> CampaignsCreated =
        Meter.CreateCounter<long>("campaignmanager.campaigns.created", "campaigns", "Campaigns submitted");

    public static readonly Counter<long> CampaignsCompleted =
        Meter.CreateCounter<long>("campaignmanager.campaigns.completed", "campaigns", "Campaigns finalized, tagged by status");

    public static readonly Histogram<double> ProviderSendDuration =
        Meter.CreateHistogram<double>("campaignmanager.provider.send.duration", "ms", "Time spent in a single provider send call");

    public static readonly Counter<long> WebhooksReceived =
        Meter.CreateCounter<long>("campaignmanager.webhooks.received", "webhooks", "Provider delivery callbacks received");
}
