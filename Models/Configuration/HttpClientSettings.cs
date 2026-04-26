namespace VseinstrumentiParser.Models.Configuration
{
    /// <summary>
    /// Настройки HTTP-клиента с политикой устойчивости
    /// </summary>
    public class HttpClientSettings
    {
        /// <summary>
        /// Базовый таймаут запроса (сек)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// User-Agent для запросов
        /// </summary>
        public string UserAgent { get; set; } = 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        /// <summary>
        /// Количество повторных попыток
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Начальная задержка для экспоненциального бэкоффа (мс)
        /// </summary>
        public int InitialRetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Максимальная задержка между ретраями (мс)
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 10000;

        /// <summary>
        /// Интервал circuit breaker (минуты)
        /// </summary>
        public int CircuitBreakerDurationMinutes { get; set; } = 5;

        /// <summary>
        /// Порог срабатывания circuit breaker (количество ошибок)
        /// </summary>
        public int CircuitBreakerExceptionCount { get; set; } = 5;

        /// <summary>
        /// Задержка между запросами к одному домену (мс)
        /// </summary>
        public int RequestDelayMs { get; set; } = 1000;
    }
}
