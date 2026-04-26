using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace VseinstrumentiParser.Services.HealthChecks
{
    /// <summary>
    /// Health check для проверки подключения к Redis
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly ILogger<RedisHealthCheck> _logger;

        public RedisHealthCheck(ConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                
                // Пробуем выполнить простую команду PING
                var latency = await db.PingAsync();
                
                _logger.LogDebug("Redis ping successful, latency: {Latency}ms", latency.TotalMilliseconds);
                
                return HealthCheckResult.Healthy(
                    "Redis подключён",
                    new Dictionary<string, object>
                    {
                        ["latency_ms"] = latency.TotalMilliseconds,
                        ["server"] = _redis.GetEndPoints().First().ToString()
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                
                return HealthCheckResult.Unhealthy(
                    "Redis недоступен",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
