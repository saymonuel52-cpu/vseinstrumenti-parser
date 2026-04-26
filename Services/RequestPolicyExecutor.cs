using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Результат выполнения HTTP-запроса с метаданными
    /// </summary>
    public class RequestResult
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public Exception? Exception { get; set; }
        public int RetryCount { get; set; }
        public TimeSpan Duration { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// Исполнитель политик устойчивости для HTTP-запросов
    /// Отделяет логику retry/timeout/circuit-breaker от логики парсинга
    /// </summary>
    public class RequestPolicyExecutor
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClientSettings _settings;
        private readonly ILogger<RequestPolicyExecutor> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

        public RequestPolicyExecutor(
            IHttpClientFactory httpClientFactory,
            HttpClientSettings settings,
            ILogger<RequestPolicyExecutor> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Retry policy с экспоненциальным бэкоффом
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    _settings.MaxRetryAttempts,
                    attempt => TimeSpan.FromMilliseconds(
                        Math.Min(_settings.InitialRetryDelayMs * Math.Pow(2, attempt - 1), _settings.MaxRetryDelayMs)),
                    onRetry: (exception, timeSpan, attempt, context) =>
                    {
                        _logger.LogWarning(
                            "Retry attempt {Attempt} for {Url}: {Error}. Waiting {Delay}ms",
                            attempt,
                            context.OperationKey,
                            exception.Message,
                            timeSpan.TotalMilliseconds);
                    });

            // Timeout policy
            _timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            // Circuit Breaker policy
            _circuitBreakerPolicy = Policy
                .Handle<HttpRequestException>()
                .CircuitBreakerAsync(
                    _settings.CircuitBreakerExceptionCount,
                    TimeSpan.FromMinutes(_settings.CircuitBreakerDurationMinutes),
                    onBreak: (exception, timespan) =>
                    {
                        _logger.LogError(
                            "Circuit breaker opened for {Duration} due to: {Error}",
                            timespan,
                            exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open: testing...");
                    });
        }

        /// <summary>
        /// Выполнить GET-запрос с политиками устойчивости
        /// </summary>
        public async Task<RequestResult> ExecuteGetAsync(string url, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var context = new Context(url);
            var httpClient = _httpClientFactory.CreateClient("ParserClient");

            _logger.LogInformation("Executing request to {Url}", url);

            try
            {
                var html = await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    await _timeoutPolicy.ExecuteAsync(async () =>
                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            var response = await httpClient.GetAsync(url, cancellationToken);
                            response.EnsureSuccessStatusCode();
                            return await response.Content.ReadAsStringAsync(cancellationToken);
                        })));

                stopwatch.Stop();

                var result = new RequestResult
                {
                    Success = true,
                    Content = html,
                    Duration = stopwatch.Elapsed,
                    Url = url,
                    RetryCount = 0
                };

                _logger.LogInformation(
                    "Request to {Url} completed successfully in {Duration}ms, size: {Size} bytes",
                    url,
                    result.Duration.TotalMilliseconds,
                    html.Length);

                return result;
            }
            catch (CircuitBreakerOpenException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Circuit breaker is open. Request blocked for {Url}", url);

                return new RequestResult
                {
                    Success = false,
                    Exception = ex,
                    Duration = stopwatch.Elapsed,
                    Url = url
                };
            }
            catch (TimeoutRejectedException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Request timed out for {Url} after {Timeout} seconds", url, _settings.TimeoutSeconds);

                return new RequestResult
                {
                    Success = false,
                    Exception = ex,
                    Duration = stopwatch.Elapsed,
                    Url = url
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to execute request to {Url} after all retries", url);

                return new RequestResult
                {
                    Success = false,
                    Exception = ex,
                    Duration = stopwatch.Elapsed,
                    Url = url
                };
            }
        }

        /// <summary>
        /// Выполнить GET-запрос с кастомными заголовками
        /// </summary>
        public async Task<RequestResult> ExecuteGetAsync(string url, Dictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var context = new Context(url);
            var httpClient = _httpClientFactory.CreateClient("ParserClient");

            // Apply custom headers
            foreach (var header in headers)
            {
                httpClient.DefaultRequestHeaders.Remove(header.Key);
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            _logger.LogInformation("Executing request to {Url} with custom headers", url);

            try
            {
                var html = await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    await _timeoutPolicy.ExecuteAsync(async () =>
                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            var response = await httpClient.GetAsync(url, cancellationToken);
                            response.EnsureSuccessStatusCode();
                            return await response.Content.ReadAsStringAsync(cancellationToken);
                        })));

                stopwatch.Stop();

                var result = new RequestResult
                {
                    Success = true,
                    Content = html,
                    Duration = stopwatch.Elapsed,
                    Url = url,
                    RetryCount = 0
                };

                _logger.LogInformation(
                    "Request to {Url} completed successfully in {Duration}ms",
                    url,
                    result.Duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to execute request to {Url}", url);

                return new RequestResult
                {
                    Success = false,
                    Exception = ex,
                    Duration = stopwatch.Elapsed,
                    Url = url
                };
            }
        }

        /// <summary>
        /// Проверить доступность URL без загрузки контента
        /// </summary>
        public async Task<bool> CheckUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("ParserClient");
                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
