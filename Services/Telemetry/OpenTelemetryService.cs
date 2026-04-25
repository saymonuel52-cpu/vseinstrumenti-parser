using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;

namespace VseinstrumentiParser.Services.Telemetry
{
    /// <summary>
    /// Сервис для работы с OpenTelemetry (трассировка, метрики, логи)
    /// </summary>
    public class OpenTelemetryService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly TelemetrySettings _settings;
        private readonly TracerProvider? _tracerProvider;
        private readonly MeterProvider? _meterProvider;
        private readonly ActivitySource _activitySource;
        private readonly Meter _meter;
        private bool _disposed = false;

        // Метрики
        private readonly Counter<long> _productsParsedCounter;
        private readonly Counter<long> _requestsCounter;
        private readonly Counter<long> _errorsCounter;
        private readonly Histogram<double> _requestDurationHistogram;
        private readonly ObservableGauge<long> _cacheHitRateGauge;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;

        /// <summary>
        /// Конструктор
        /// </summary>
        public OpenTelemetryService(ILogger logger, TelemetrySettings settings)
        {
            _logger = logger;
            _settings = settings;
            
            // Создаем ActivitySource и Meter
            _activitySource = new ActivitySource(_settings.ServiceName);
            _meter = new Meter(_settings.ServiceName);

            // Инициализируем метрики
            _productsParsedCounter = _meter.CreateCounter<long>(
                name: "parser.products.parsed",
                unit: "items",
                description: "Количество распарсенных товаров");

            _requestsCounter = _meter.CreateCounter<long>(
                name: "parser.http.requests",
                unit: "requests",
                description: "Количество HTTP запросов");

            _errorsCounter = _meter.CreateCounter<long>(
                name: "parser.errors.total",
                unit: "errors",
                description: "Количество ошибок");

            _requestDurationHistogram = _meter.CreateHistogram<double>(
                name: "parser.http.request.duration",
                unit: "ms",
                description: "Длительность HTTP запросов");

            _cacheHitRateGauge = _meter.CreateObservableGauge<long>(
                name: "parser.cache.hit.rate",
                unit: "percent",
                description: "Процент попаданий в кэш",
                observeValues: () => new[]
                {
                    new Measurement<long>(
                        value: CalculateCacheHitRate(),
                        tags: new KeyValuePair<string, object?>("cache.type", "distributed"))
                });

            // Настраиваем провайдеры OpenTelemetry если включено
            if (_settings.Enabled)
            {
                ConfigureOpenTelemetry();
                _logger.LogInformation($"OpenTelemetry инициализирован. Service: {_settings.ServiceName}, Endpoint: {_settings.Endpoint}");
            }
            else
            {
                _logger.LogInformation("OpenTelemetry отключен в настройках");
            }
        }

        /// <summary>
        /// Настройка OpenTelemetry
        /// </summary>
        private void ConfigureOpenTelemetry()
        {
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName: _settings.ServiceName, serviceVersion: _settings.ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["environment"] = _settings.Environment,
                    ["deployment.region"] = _settings.DeploymentRegion
                });

            // Настройка трассировки
            if (_settings.TracingEnabled)
            {
                _tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(_settings.ServiceName)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter() // Для отладки
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(_settings.Endpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    })
                    .Build();
            }

            // Настройка метрик
            if (_settings.MetricsEnabled)
            {
                _meterProvider = Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(_settings.ServiceName)
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter() // Для отладки
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(_settings.Endpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    })
                    .Build();
            }
        }

        /// <summary>
        /// Начать трассировку операции
        /// </summary>
        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            if (!_settings.Enabled || !_settings.TracingEnabled)
                return null;

            return _activitySource.StartActivity(name, kind);
        }

        /// <summary>
        /// Зарегистрировать распарсенный товар
        /// </summary>
        public void RecordProductParsed(int count = 1, Dictionary<string, object?>? tags = null)
        {
            if (!_settings.Enabled || !_settings.MetricsEnabled)
                return;

            var tagList = CreateTags(tags);
            _productsParsedCounter.Add(count, tagList);
        }

        /// <summary>
        /// Зарегистрировать HTTP запрос
        /// </summary>
        public void RecordHttpRequest(string method, string url, int statusCode, double durationMs, Dictionary<string, object?>? tags = null)
        {
            if (!_settings.Enabled || !_settings.MetricsEnabled)
                return;

            var tagList = CreateTags(tags);
            tagList.Add(new KeyValuePair<string, object?>("http.method", method));
            tagList.Add(new KeyValuePair<string, object?>("http.url", url));
            tagList.Add(new KeyValuePair<string, object?>("http.status_code", statusCode));

            _requestsCounter.Add(1, tagList);
            _requestDurationHistogram.Record(durationMs, tagList);
        }

        /// <summary>
        /// Зарегистрировать ошибку
        /// </summary>
        public void RecordError(string errorType, string? message = null, Dictionary<string, object?>? tags = null)
        {
            if (!_settings.Enabled || !_settings.MetricsEnabled)
                return;

            var tagList = CreateTags(tags);
            tagList.Add(new KeyValuePair<string, object?>("error.type", errorType));
            
            if (!string.IsNullOrEmpty(message))
            {
                tagList.Add(new KeyValuePair<string, object?>("error.message", message));
            }

            _errorsCounter.Add(1, tagList);
        }

        /// <summary>
        /// Зарегистрировать попадание в кэш
        /// </summary>
        public void RecordCacheHit()
        {
            Interlocked.Increment(ref _cacheHits);
        }

        /// <summary>
        /// Зарегистрировать промах кэша
        /// </summary>
        public void RecordCacheMiss()
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        /// <summary>
        /// Рассчитать процент попаданий в кэш
        /// </summary>
        private long CalculateCacheHitRate()
        {
            var total = _cacheHits + _cacheMisses;
            if (total == 0)
                return 0;

            return (long)((_cacheHits / (double)total) * 100);
        }

        /// <summary>
        /// Создать теги для метрик
        /// </summary>
        private TagList CreateTags(Dictionary<string, object?>? additionalTags)
        {
            var tags = new TagList
            {
                { "service.name", _settings.ServiceName },
                { "service.version", _settings.ServiceVersion },
                { "environment", _settings.Environment }
            };

            if (additionalTags != null)
            {
                foreach (var tag in additionalTags)
                {
                    tags.Add(tag.Key, tag.Value);
                }
            }

            return tags;
        }

        /// <summary>
        /// Получить текущие метрики
        /// </summary>
        public TelemetryMetrics GetCurrentMetrics()
        {
            return new TelemetryMetrics
            {
                ProductsParsed = GetCounterValue(_productsParsedCounter),
                HttpRequests = GetCounterValue(_requestsCounter),
                Errors = GetCounterValue(_errorsCounter),
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                CacheHitRate = CalculateCacheHitRate()
            };
        }

        /// <summary>
        /// Получить значение счетчика (для демонстрации)
        /// </summary>
        private long GetCounterValue(Counter<long> counter)
        {
            // В реальной реализации здесь будет логика получения текущего значения
            // Для простоты возвращаем 0
            return 0;
        }

        /// <summary>
        /// Экспорт метрик в Prometheus формате
        /// </summary>
        public string ExportMetricsAsPrometheus()
        {
            var metrics = GetCurrentMetrics();
            
            var prometheusLines = new List<string>
            {
                "# HELP parser_products_parsed_total Total number of parsed products",
                "# TYPE parser_products_parsed_total counter",
                $"parser_products_parsed_total {metrics.ProductsParsed}",
                
                "# HELP parser_http_requests_total Total number of HTTP requests",
                "# TYPE parser_http_requests_total counter",
                $"parser_http_requests_total {metrics.HttpRequests}",
                
                "# HELP parser_errors_total Total number of errors",
                "# TYPE parser_errors_total counter",
                $"parser_errors_total {metrics.Errors}",
                
                "# HELP parser_cache_hits_total Total number of cache hits",
                "# TYPE parser_cache_hits_total counter",
                $"parser_cache_hits_total {metrics.CacheHits}",
                
                "# HELP parser_cache_misses_total Total number of cache misses",
                "# TYPE parser_cache_misses_total counter",
                $"parser_cache_misses_total {metrics.CacheMisses}",
                
                "# HELP parser_cache_hit_rate_percent Cache hit rate percentage",
                "# TYPE parser_cache_hit_rate_percent gauge",
                $"parser_cache_hit_rate_percent {metrics.CacheHitRate}"
            };
            
            return string.Join("\n", prometheusLines);
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _tracerProvider?.Dispose();
                _meterProvider?.Dispose();
                _activitySource.Dispose();
                _meter.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Настройки телеметрии
    /// </summary>
    public class TelemetrySettings
    {
        public bool Enabled { get; set; } = false;
        public bool TracingEnabled { get; set; } = true;
        public bool MetricsEnabled { get; set; } = true;
        public bool LoggingEnabled { get; set; } = true;
        public string ServiceName { get; set; } = "vseinstrumenti-parser";
        public string ServiceVersion { get; set; } = "1.0.0";
        public string Environment { get; set; } = "production";
        public string DeploymentRegion { get; set; } = "ru-central1";
        public string Endpoint { get; set; } = "http://localhost:4317";
        public string Exporter { get; set; } = "otlp"; // otlp, prometheus, console
    }

    /// <summary>
    /// Метрики телеметрии
    /// </summary>
    public class TelemetryMetrics
    {
        public long ProductsParsed { get; set; }
        public long HttpRequests { get; set; }
        public long Errors { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public long CacheHitRate { get; set; }
        
        public double RequestsPerMinute { get; set; }
        public double ErrorRate { get; set; }
        public double AverageRequestDurationMs { get; set; }
    }

    /// <summary>
    /// Расширения для регистрации в DI
    /// </summary>
    public static class OpenTelemetryExtensions
    {
        public static IServiceCollection AddOpenTelemetryService(
            this IServiceCollection services,
            TelemetrySettings settings)
        {
            services.AddSingleton(settings);
            services.AddSingleton<OpenTelemetryService>();
            
            return services;
        }

        public static IServiceCollection AddOpenTelemetryWithConsoleExporter(
            this IServiceCollection services,
            string serviceName = "vseinstrumenti-parser")
        {
            var settings = new TelemetrySettings
            {
                Enabled = true,
                ServiceName = serviceName,
                Endpoint = "http://localhost:4317",
                Exporter = "console"
            };
            
            return services.AddOpenTelemetryService(settings);
        }

        public static IServiceCollection AddOpenTelemetryWithPrometheus(
            this IServiceCollection services,
            string serviceName = "vseinstrumenti-parser")
        {
            var settings = new TelemetrySettings
            {
                Enabled = true,
                ServiceName = serviceName,
                Endpoint = "http://localhost:9090",
                Exporter = "prometheus"
            };
            
            return services.AddOpenTelemetryService(settings);
        }
    }
}