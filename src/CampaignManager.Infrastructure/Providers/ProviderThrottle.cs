using System.Collections.Concurrent;
using CampaignManager.Application.Providers;

namespace CampaignManager.Infrastructure.Providers;

/// <summary>In-process sliding-window throttle per provider configuration. Sufficient for a
/// single Workers instance; multi-instance fleets should move this to Redis (Phase 3).</summary>
public sealed class ProviderThrottle : IProviderThrottle
{
    private sealed class Window
    {
        public readonly object Lock = new();
        public readonly Queue<DateTime> Sends = new();
    }

    private readonly ConcurrentDictionary<Guid, Window> _windows = new();

    public async Task WaitAsync(Guid providerConfigurationId, int? rateLimitPerMinute, CancellationToken ct)
    {
        if (rateLimitPerMinute is not > 0) return;
        var window = _windows.GetOrAdd(providerConfigurationId, _ => new Window());

        while (true)
        {
            TimeSpan wait;
            lock (window.Lock)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-1);
                while (window.Sends.TryPeek(out var oldest) && oldest < cutoff)
                {
                    window.Sends.Dequeue();
                }

                if (window.Sends.Count < rateLimitPerMinute.Value)
                {
                    window.Sends.Enqueue(DateTime.UtcNow);
                    return;
                }

                wait = window.Sends.Peek().AddMinutes(1) - DateTime.UtcNow;
            }

            await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(50), ct);
        }
    }
}
