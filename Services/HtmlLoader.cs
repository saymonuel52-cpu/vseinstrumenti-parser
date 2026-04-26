using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Реализация загрузчика HTML с политикой устойчивости через RequestPolicyExecutor
    /// </summary>
    public class HtmlLoader : IHtmlLoader
    {
        private readonly RequestPolicyExecutor _policyExecutor;
        private readonly HttpClientSettings _settings;
        private readonly ILogger<HtmlLoader> _logger;

        public HtmlLoader(
            RequestPolicyExecutor policyExecutor,
            HttpClientSettings settings,
            ILogger<HtmlLoader> logger)
        {
            _policyExecutor = policyExecutor ?? throw new ArgumentNullException(nameof(policyExecutor));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> LoadHtmlAsync(string url, CancellationToken cancellationToken = default)
        {
            return await LoadHtmlAsync(url, null, cancellationToken);
        }

        public async Task<string> LoadHtmlAsync(string url, Dictionary<string, string>? headers, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Loading HTML from {Url}", url);

            RequestResult result;

            if (headers != null && headers.Count > 0)
            {
                result = await _policyExecutor.ExecuteGetAsync(url, headers, cancellationToken);
            }
            else
            {
                result = await _policyExecutor.ExecuteGetAsync(url, cancellationToken);
            }

            if (!result.Success)
            {
                var ex = result.Exception ?? new HttpRequestException($"Failed to load {url}");
                _logger.LogError(ex, "Failed to load HTML from {Url} after all retries", url);
                throw new HttpRequestException($"Failed to load {url} after {_settings.MaxRetryAttempts} attempts", ex);
            }

            _logger.LogInformation("Successfully loaded {Length} bytes from {Url}", result.Content?.Length ?? 0, url);
            return result.Content ?? string.Empty;
        }
    }
}
