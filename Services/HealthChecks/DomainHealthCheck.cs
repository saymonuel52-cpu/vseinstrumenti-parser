using Microsoft.Extensions.Diagnostics.HealthChecks;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services.HealthChecks
{
    /// <summary>
    /// Health check для проверки доступности целевых доменов
    /// </summary>
    public class DomainHealthCheck : IHealthCheck
    {
        private readonly IHtmlLoader _htmlLoader;
        private readonly HttpClientSettings _settings;
        private readonly ILogger<DomainHealthCheck> _logger;

        public DomainHealthCheck(IHtmlLoader htmlLoader, HttpClientSettings settings, ILogger<DomainHealthCheck> logger)
        {
            _htmlLoader = htmlLoader ?? throw new ArgumentNullException(nameof(htmlLoader));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var domains = new[]
            {
                "https://www.vseinstrumenti.ru",
                "https://220-volt.ru"
            };

            var unhealthyDomains = new List<string>();
            var totalLatency = TimeSpan.Zero;

            foreach (var domain in domains)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await _htmlLoader.LoadHtmlAsync(domain, cancellationToken);
                    stopwatch.Stop();

                    totalLatency += stopwatch.Elapsed;
                    _logger.LogDebug("Domain {Domain} is healthy, latency: {Latency}ms", domain, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Domain {Domain} is unhealthy", domain);
                    unhealthyDomains.Add(domain);
                }
            }

            if (unhealthyDomains.Count == domains.Length)
            {
                return HealthCheckResult.Unhealthy(
                    "Все целевые домены недоступны",
                    null,
                    new Dictionary<string, object>
                    {
                        ["unhealthy_domains"] = unhealthyDomains,
                        ["total_latency_ms"] = totalLatency.TotalMilliseconds
                    });
            }

            if (unhealthyDomains.Count > 0)
            {
                return HealthCheckResult.Degraded(
                    $"Недоступны домены: {string.Join(", ", unhealthyDomains)}",
                    null,
                    new Dictionary<string, object>
                    {
                        ["unhealthy_domains"] = unhealthyDomains,
                        ["healthy_domains"] = domains.Except(unhealthyDomains),
                        ["total_latency_ms"] = totalLatency.TotalMilliseconds
                    });
            }

            return HealthCheckResult.Healthy(
                "Все домены доступны",
                new Dictionary<string, object>
                {
                    ["total_latency_ms"] = totalLatency.TotalMilliseconds,
                    ["checked_domains"] = domains.Length
                });
        }
    }
}
