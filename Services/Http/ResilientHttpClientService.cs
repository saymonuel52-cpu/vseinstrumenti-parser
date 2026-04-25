using System.Net;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using Polly.CircuitBreaker;
using Microsoft.Extensions.Logging;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services.Http
{
    /// <summary>
    /// Устойчивый HTTP-клиент с Polly для retry, rate-limiting и circuit breaker
    /// </summary>
    public class ResilientHttpClientService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly RequestSettings _settings;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly List<string> _userAgents;
        private readonly Random _random;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с IHttpClientFactory
        /// </summary>
        public ResilientHttpClientService(
            IHttpClientFactory httpClientFactory,
            ILogger logger,
            RequestSettings settings)
        {
            _logger = logger;
            _settings = settings;
            _random = new Random();
            
            // Инициализация списка User-Agent для ротации
            _userAgents = new List<string>
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/121.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0 Safari/537.36"
            };

            // Создаем HTTP-клиент через фабрику
            _httpClient = httpClientFactory.CreateClient("ParserHttpClient");
            ConfigureHttpClient();

            // Настраиваем политики Polly
            _retryPolicy = CreateRetryPolicy();
            _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
            _timeoutPolicy = CreateTimeoutPolicy();
            
            _logger.LogInformation($"ResilientHttpClientService инициализирован. Timeout: {_settings.TimeoutSeconds} сек, MaxRetries: {_settings.MaxRetries}");
        }

        /// <summary>
        /// Конфигурация HTTP-клиента
        /// </summary>
        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Clear();
            
            // Устанавливаем случайный User-Agent
            var userAgent = GetRandomUserAgent();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            
            // Стандартные заголовки
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            
            // Добавляем Referer для имитации реального браузера
            _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.google.com/");
        }

        /// <summary>
        /// Создание политики повторных попыток
        /// </summary>
        private AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .OrResult(msg => 
                    msg.StatusCode == HttpStatusCode.TooManyRequests || // 429
                    msg.StatusCode == HttpStatusCode.RequestTimeout ||   // 408
                    msg.StatusCode == HttpStatusCode.BadGateway ||       // 502
                    msg.StatusCode == HttpStatusCode.ServiceUnavailable || // 503
                    msg.StatusCode == HttpStatusCode.GatewayTimeout)     // 504
                .WaitAndRetryAsync(
                    retryCount: _settings.MaxRetries,
                    sleepDurationProvider: retryAttempt => 
                    {
                        // Exponential backoff with jitter
                        var baseDelay = TimeSpan.FromMilliseconds(_settings.RetryDelayMs * Math.Pow(2, retryAttempt - 1));
                        var jitter = TimeSpan.FromMilliseconds(_random.Next(0, 500));
                        return baseDelay + jitter;
                    },
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        _logger.LogWarning($"Повторная попытка {retryAttempt}/{_settings.MaxRetries} через {timespan.TotalSeconds:F1} сек. " +
                                          $"Причина: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                    });
        }

        /// <summary>
        /// Создание политики Circuit Breaker
        /// </summary>
        private AsyncCircuitBreakerPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay) =>
                    {
                        _logger.LogError($"Circuit Breaker открыт на {breakDelay.TotalSeconds} сек. " +
                                        $"Причина: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit Breaker закрыт, запросы возобновлены");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit Breaker в полуоткрытом состоянии, тестирование соединения");
                    });
        }

        /// <summary>
        /// Создание политики таймаута
        /// </summary>
        private AsyncTimeoutPolicy CreateTimeoutPolicy()
        {
            return Policy.TimeoutAsync(TimeSpan.FromSeconds(_settings.TimeoutSeconds), 
                TimeoutStrategy.Optimistic, 
                onTimeoutAsync: (context, timespan, task) =>
                {
                    _logger.LogWarning($"Таймаут запроса: {timespan.TotalSeconds} сек");
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Получить HTML-контент по URL с применением всех политик
        /// </summary>
        public async Task<string> GetHtmlContentAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                // Комбинируем политики: Timeout -> Circuit Breaker -> Retry
                var policyWrap = Policy.WrapAsync(_timeoutPolicy, _circuitBreakerPolicy, _retryPolicy);
                
                var response = await policyWrap.ExecuteAsync(async (ct) =>
                {
                    // Добавляем задержку между запросами для rate limiting
                    await Task.Delay(_settings.DelayBetweenRequestsMs, ct);
                    
                    // Ротация User-Agent для каждого запроса
                    RotateUserAgent();
                    
                    _logger.LogDebug($"Запрос: {url}");
                    return await _httpClient.GetAsync(url, ct);
                }, cancellationToken);

                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug($"Ответ получен: {url}, размер: {content.Length} байт");
                
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при запросе {url}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получить случайный User-Agent
        /// </summary>
        private string GetRandomUserAgent()
        {
            return _userAgents[_random.Next(_userAgents.Count)];
        }

        /// <summary>
        /// Ротация User-Agent
        /// </summary>
        private void RotateUserAgent()
        {
            var newUserAgent = GetRandomUserAgent();
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(newUserAgent);
            
            // Также меняем другие заголовки для разнообразия
            if (_random.NextDouble() > 0.5)
            {
                _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.yandex.ru/");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.google.com/");
            }
        }

        /// <summary>
        /// Выполнить POST запрос
        /// </summary>
        public async Task<string> PostAsync(string url, HttpContent content, CancellationToken cancellationToken = default)
        {
            try
            {
                var policyWrap = Policy.WrapAsync(_timeoutPolicy, _circuitBreakerPolicy, _retryPolicy);
                
                var response = await policyWrap.ExecuteAsync(async (ct) =>
                {
                    await Task.Delay(_settings.DelayBetweenRequestsMs, ct);
                    RotateUserAgent();
                    
                    _logger.LogDebug($"POST запрос: {url}");
                    return await _httpClient.PostAsync(url, content, ct);
                }, cancellationToken);

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при POST запросе {url}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получить статистику использования
        /// </summary>
        public HttpClientStatistics GetStatistics()
        {
            // В реальном проекте здесь можно собирать метрики из Polly
            return new HttpClientStatistics
            {
                UserAgentCount = _userAgents.Count,
                TimeoutSeconds = _settings.TimeoutSeconds,
                MaxRetries = _settings.MaxRetries,
                DelayBetweenRequestsMs = _settings.DelayBetweenRequestsMs
            };
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Статистика HTTP-клиента
    /// </summary>
    public class HttpClientStatistics
    {
        public int UserAgentCount { get; set; }
        public int TimeoutSeconds { get; set; }
        public int MaxRetries { get; set; }
        public int DelayBetweenRequestsMs { get; set; }
    }

    /// <summary>
    /// Расширения для регистрации в DI
    /// </summary>
    public static class HttpClientServiceExtensions
    {
        public static IServiceCollection AddResilientHttpClient(this IServiceCollection services, RequestSettings settings)
        {
            services.AddHttpClient("ParserHttpClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseProxy = true,
                Proxy = null
            })
            .AddPolicyHandler((services, request) =>
            {
                // Добавляем базовую политику retry через Polly
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(3, retryAttempt => 
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
            });

            services.AddSingleton<ResilientHttpClientService>();
            
            return services;
        }
    }
}