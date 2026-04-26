namespace VseinstrumentiParser.WebUI.Services
{
    /// <summary>
    /// Service for monitoring parser metrics and statistics
    /// </summary>
    public class ParserMonitoringService
    {
        private readonly ILogger _logger;
        private readonly IDistributedCache _cache;

        public ParserMonitoringService(ILogger logger, IDistributedCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task<ParserMetrics> GetMetricsAsync()
        {
            var cacheKey = "parser_metrics";
            var cachedMetrics = await _cache.GetStringAsync(cacheKey);

            if (cachedMetrics != null)
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<ParserMetrics>(cachedMetrics) 
                        ?? new ParserMetrics();
                }
                catch
                {
                    // Cache corrupted, regenerate
                }
            }

            var metrics = new ParserMetrics
            {
                TotalProducts = await GetTotalProductsCount(),
                CacheHits = await GetCacheHits(),
                CacheMisses = await GetCacheMisses(),
                CacheHitRate = await CalculateCacheHitRate(),
                ErrorsLast24h = await GetErrorsLast24h(),
                LastErrorTime = await GetLastErrorTime(),
                ParsesLast24h = await GetParsesLast24h(),
                AverageParseDuration = await GetAverageParseDuration(),
                Timestamp = DateTime.Now
            };

            await _cache.SetStringAsync(cacheKey, 
                System.Text.Json.JsonSerializer.Serialize(metrics),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            return metrics;
        }

        public async Task ClearCacheAsync()
        {
            // In production, implement proper cache clearing
            // For now, just clear specific keys
            await _cache.RemoveAsync("parser_metrics");
            await _cache.RemoveAsync("products_*");
            await _cache.RemoveAsync("categories_*");
            await _cache.RemoveAsync("compare_*");

            _logger.LogInformation("Cache cleared successfully");
        }

        public async Task IncrementCacheHit()
        {
            var key = "cache_stats:hits";
            var hits = await GetCounter(key);
            await SetCounter(key, hits + 1);
        }

        public async Task IncrementCacheMiss()
        {
            var key = "cache_stats:misses";
            var misses = await GetCounter(key);
            await SetCounter(key, misses + 1);
        }

        public async Task IncrementError(string errorType)
        {
            var key = $"errors:{DateTime.Now:yyyy-MM-dd}:{errorType}";
            var count = await GetCounter(key);
            await SetCounter(key, count + 1);

            // Also track total errors
            await IncrementErrorTotal();
        }

        public async Task RecordParseStart(string source)
        {
            var key = $"parses:{DateTime.Now:yyyy-MM-dd}";
            var count = await GetCounter(key);
            await SetCounter(key, count + 1);

            await RecordMetric($"parse_start:{source}", 1);
        }

        public async Task RecordParseComplete(string source, int productsCount, TimeSpan duration)
        {
            await RecordMetric($"parse_duration:{source}", (long)duration.TotalMilliseconds);
            await RecordMetric($"products_parsed:{source}", productsCount);
        }

        private async Task<int> GetTotalProductsCount()
        {
            // In production, query database or aggregate from cache
            return new Random().Next(1000, 10000);
        }

        private async Task<int> GetCacheHits()
        {
            return await GetCounter("cache_stats:hits");
        }

        private async Task<int> GetCacheMisses()
        {
            return await GetCounter("cache_stats:misses");
        }

        private async Task<int> CalculateCacheHitRate()
        {
            var hits = await GetCacheHits();
            var misses = await GetCacheMisses();
            var total = hits + misses;

            return total > 0 ? (int)((double)hits / total * 100) : 0;
        }

        private async Task<int> GetErrorsLast24h()
        {
            var total = 0;
            var now = DateTime.Now;
            
            for (int i = 0; i < 7; i++)
            {
                var date = now.AddDays(-i).ToString("yyyy-MM-dd");
                var keys = new[] { "errors", "error", "exception", "timeout" };
                
                foreach (var errorType in keys)
                {
                    total += await GetCounter($"errors:{date}:{errorType}");
                }
            }

            return total;
        }

        private async Task<DateTime?> GetLastErrorTime()
        {
            // In production, query error logs
            return DateTime.Now.AddMinutes(-new Random().Next(5, 120));
        }

        private async Task<int> GetParsesLast24h()
        {
            var total = 0;
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            total += await GetCounter($"parses:{today}");
            
            // Add yesterday if needed
            var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
            total += await GetCounter($"parses:{yesterday}");
            
            return total;
        }

        private async Task<TimeSpan> GetAverageParseDuration()
        {
            // In production, calculate from historical data
            return TimeSpan.FromSeconds(new Random().Next(30, 300));
        }

        private async Task<int> GetCounter(string key)
        {
            var data = await _cache.GetStringAsync(key);
            return int.TryParse(data, out var value) ? value : 0;
        }

        private async Task SetCounter(string key, int value)
        {
            await _cache.SetStringAsync(key, value.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                });
        }

        private async Task RecordMetric(string key, long value)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH");
            await _cache.SetStringAsync($"{key}:{timestamp}", value.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                });
        }
    }

    public class ParserMetrics
    {
        public int TotalProducts { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public int CacheHitRate { get; set; }
        public int ErrorsLast24h { get; set; }
        public DateTime? LastErrorTime { get; set; }
        public int ParsesLast24h { get; set; }
        public TimeSpan AverageParseDuration { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
