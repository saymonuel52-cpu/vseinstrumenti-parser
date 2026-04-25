using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace VseinstrumentiParser.Services.Health
{
    /// <summary>
    /// Health check для Redis
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisHealthCheck(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var pingResult = await db.PingAsync();
                
                if (pingResult.TotalMilliseconds > 1000)
                {
                    return HealthCheckResult.Degraded(
                        $"Redis ping high latency: {pingResult.TotalMilliseconds}ms",
                        new Exception($"Redis latency is {pingResult.TotalMilliseconds}ms"));
                }

                return HealthCheckResult.Healthy($"Redis is healthy (ping: {pingResult.TotalMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis connection failed", ex);
            }
        }
    }
}