using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VseinstrumentiParser.Services.Health
{
    /// <summary>
    /// Health check для проверки доступности внешних сайтов
    /// </summary>
    public class ExternalSitesHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExternalSitesHealthCheck> _logger;

        public ExternalSitesHealthCheck(
            IHttpClientFactory httpClientFactory,
            ILogger<ExternalSitesHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var sites = new[]
            {
                "https://www.vseinstrumenti.ru",
                "https://www.220-volt.ru"
            };

            var results = new List<(string Site, bool IsHealthy, long ResponseTimeMs, string? Error)>();

            foreach (var site in sites)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("HealthCheck");
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await client.GetAsync(site, cancellationToken);
                    stopwatch.Stop();
                    
                    var isHealthy = response.IsSuccessStatusCode;
                    results.Add((site, isHealthy, stopwatch.ElapsedMilliseconds, 
                        isHealthy ? null : $"HTTP {response.StatusCode}"));
                    
                    _logger.LogDebug("Health check for {Site}: {Status} ({ResponseTime}ms)", 
                        site, isHealthy ? "Healthy" : "Unhealthy", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    results.Add((site, false, 0, ex.Message));
                    _logger.LogWarning(ex, "Health check failed for {Site}", site);
                }
            }

            var healthySites = results.Count(r => r.IsHealthy);
            var totalSites = sites.Length;
            
            if (healthySites == totalSites)
            {
                return HealthCheckResult.Healthy(
                    $"All external sites are accessible. Response times: {string.Join(", ", results.Select(r => $"{r.Site}: {r.ResponseTimeMs}ms"))}");
            }
            else if (healthySites >= totalSites / 2)
            {
                var unhealthySites = results.Where(r => !r.IsHealthy).Select(r => r.Site);
                return HealthCheckResult.Degraded(
                    $"{healthySites}/{totalSites} sites accessible. Unhealthy: {string.Join(", ", unhealthySites)}",
                    new Exception($"Partial availability: {healthySites}/{totalSites}"));
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    $"Only {healthySites}/{totalSites} sites accessible",
                    new Exception("Majority of external sites are unavailable"));
            }
        }
    }
}