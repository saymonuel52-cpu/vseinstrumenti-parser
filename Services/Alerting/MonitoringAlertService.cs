using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VseinstrumentiParser.Services.Alerting
{
    /// <summary>
    /// Служба мониторинга метрик и отправки алертов
    /// </summary>
    public class MonitoringAlertService : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MonitoringAlertService> _logger;
        private readonly AlertService _alertService;
        private readonly List<MonitoringRule> _rules = new();
        private Timer? _monitoringTimer;
        private bool _disposed;

        public MonitoringAlertService(
            IConfiguration configuration,
            ILogger<MonitoringAlertService> logger,
            AlertService alertService)
        {
            _configuration = configuration;
            _logger = logger;
            _alertService = alertService;
            
            InitializeRules();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Запуск службы мониторинга алертов");
            
            var intervalSeconds = _configuration.GetValue<int>("Monitoring:Alerting:CheckIntervalSeconds", 60);
            _monitoringTimer = new Timer(CheckRules, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Остановка службы мониторинга алертов");
            
            _monitoringTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private void InitializeRules()
        {
            // Правила для мониторинга
            _rules.Add(new MonitoringRule
            {
                Name = "High CPU Usage",
                MetricName = "cpu_usage_percent",
                Threshold = 80,
                Comparison = ComparisonType.GreaterThan,
                Severity = AlertSeverity.Warning,
                CooldownMinutes = 30
            });

            _rules.Add(new MonitoringRule
            {
                Name = "High Memory Usage",
                MetricName = "memory_usage_percent",
                Threshold = 85,
                Comparison = ComparisonType.GreaterThan,
                Severity = AlertSeverity.Warning,
                CooldownMinutes = 30
            });

            _rules.Add(new MonitoringRule
            {
                Name = "Low Disk Space",
                MetricName = "disk_free_percent",
                Threshold = 10,
                Comparison = ComparisonType.LessThan,
                Severity = AlertSeverity.Critical,
                CooldownMinutes = 60
            });

            _rules.Add(new MonitoringRule
            {
                Name = "High Error Rate",
                MetricName = "http_error_rate",
                Threshold = 5,
                Comparison = ComparisonType.GreaterThan,
                Severity = AlertSeverity.Critical,
                CooldownMinutes = 15
            });

            _rules.Add(new MonitoringRule
            {
                Name = "High Response Time",
                MetricName = "http_response_time_ms",
                Threshold = 5000,
                Comparison = ComparisonType.GreaterThan,
                Severity = AlertSeverity.Warning,
                CooldownMinutes = 30
            });

            _rules.Add(new MonitoringRule
            {
                Name = "Low Cache Hit Rate",
                MetricName = "cache_hit_rate",
                Threshold = 70,
                Comparison = ComparisonType.LessThan,
                Severity = AlertSeverity.Info,
                CooldownMinutes = 60
            });

            // Загрузка правил из конфигурации
            var customRules = _configuration.GetSection("Monitoring:Alerting:Rules").Get<List<MonitoringRule>>();
            if (customRules != null)
            {
                _rules.AddRange(customRules);
            }
        }

        private async void CheckRules(object? state)
        {
            try
            {
                _logger.LogDebug("Проверка правил мониторинга");
                
                // В реальной реализации здесь нужно получать текущие метрики
                // из Prometheus, OpenTelemetry или другого источника
                var metrics = await GetCurrentMetricsAsync();
                
                foreach (var rule in _rules)
                {
                    if (metrics.TryGetValue(rule.MetricName, out var value))
                    {
                        var shouldAlert = rule.Comparison switch
                        {
                            ComparisonType.GreaterThan => value > rule.Threshold,
                            ComparisonType.LessThan => value < rule.Threshold,
                            ComparisonType.Equal => Math.Abs(value - rule.Threshold) < 0.01,
                            _ => false
                        };

                        if (shouldAlert && rule.CanSendAlert())
                        {
                            await SendAlertForRuleAsync(rule, value);
                            rule.LastAlertTime = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке правил мониторинга");
            }
        }

        private async Task<Dictionary<string, double>> GetCurrentMetricsAsync()
        {
            // В реальной реализации здесь нужно получать метрики из источника данных
            // Например, из Prometheus API: http://prometheus:9090/api/v1/query?query=...
            
            var metrics = new Dictionary<string, double>();
            
            // Заглушки для демонстрации
            var random = new Random();
            
            metrics["cpu_usage_percent"] = random.Next(10, 90);
            metrics["memory_usage_percent"] = random.Next(20, 95);
            metrics["disk_free_percent"] = random.Next(5, 100);
            metrics["http_error_rate"] = random.Next(0, 10);
            metrics["http_response_time_ms"] = random.Next(100, 6000);
            metrics["cache_hit_rate"] = random.Next(50, 100);
            
            // Получение реальных метрик из системы
            await Task.Delay(100); // Имитация асинхронной операции
            
            return metrics;
        }

        private async Task SendAlertForRuleAsync(MonitoringRule rule, double currentValue)
        {
            var message = $"Правило: {rule.Name}\n" +
                         $"Метрика: {rule.MetricName}\n" +
                         $"Текущее значение: {currentValue:F2}\n" +
                         $"Порог: {rule.Threshold}\n" +
                         $"Сравнение: {rule.Comparison}\n" +
                         $"Время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            switch (rule.Severity)
            {
                case AlertSeverity.Info:
                    await _alertService.SendInfoAlertAsync($"Мониторинг: {rule.Name}", message);
                    break;
                case AlertSeverity.Warning:
                    await _alertService.SendWarningAlertAsync($"Мониторинг: {rule.Name}", message);
                    break;
                case AlertSeverity.Critical:
                    await _alertService.SendErrorAlertAsync($"Мониторинг: {rule.Name}", message);
                    break;
            }
            
            _logger.LogWarning("Alert sent for rule {RuleName}: {CurrentValue} {Comparison} {Threshold}", 
                rule.Name, currentValue, rule.Comparison, rule.Threshold);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Правило мониторинга
    /// </summary>
    public class MonitoringRule
    {
        public string Name { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public ComparisonType Comparison { get; set; }
        public AlertSeverity Severity { get; set; }
        public int CooldownMinutes { get; set; }
        public DateTime? LastAlertTime { get; set; }

        public bool CanSendAlert()
        {
            if (!LastAlertTime.HasValue)
                return true;
                
            return DateTime.UtcNow - LastAlertTime.Value > TimeSpan.FromMinutes(CooldownMinutes);
        }
    }

    /// <summary>
    /// Тип сравнения
    /// </summary>
    public enum ComparisonType
    {
        GreaterThan,
        LessThan,
        Equal
    }

    /// <summary>
    /// Уровень серьезности алерта
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}