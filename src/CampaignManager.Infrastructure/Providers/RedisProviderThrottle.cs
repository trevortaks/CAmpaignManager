using CampaignManager.Application.Providers;
using StackExchange.Redis;

namespace CampaignManager.Infrastructure.Providers;

/// <summary>Distributed per-minute fixed-window throttle backed by Redis INCR, correct across
/// multiple Workers instances (unlike the single-process ProviderThrottle fallback).</summary>
public sealed class RedisProviderThrottle : IProviderThrottle
{
    private readonly IConnectionMultiplexer _redis;

    public RedisProviderThrottle(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task WaitAsync(Guid providerConfigurationId, int? rateLimitPerMinute, CancellationToken ct)
    {
        if (rateLimitPerMinute is not > 0) return;
        var db = _redis.GetDatabase();

        while (true)
        {
            var utcNow = DateTime.UtcNow;
            var bucket = utcNow.ToString("yyyyMMddHHmm");
            var key = $"throttle:{providerConfigurationId}:{bucket}";

            var count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                // Outlive the minute bucket so a slow request doesn't leave a dangling key forever.
                await db.KeyExpireAsync(key, TimeSpan.FromMinutes(2));
            }

            if (count <= rateLimitPerMinute.Value)
            {
                return;
            }

            var wait = TimeSpan.FromSeconds(61 - utcNow.Second);
            await Task.Delay(wait, ct);
        }
    }
}
