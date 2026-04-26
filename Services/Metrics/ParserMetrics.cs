using System.Collections.Concurrent;

namespace VseinstrumentiParser.Services.Metrics
{
    /// <summary>
    /// Интерфейс для сбора метрик парсера
    /// </summary>
    public interface IParserMetrics
    {
        /// <summary>
        /// Увеличить счетчик распарсенных товаров
        /// </summary>
        void IncrementProductsParsed(string source, string category);

        /// <summary>
        /// Зарегистрировать ошибку парсера
        /// </summary>
        void IncrementParserErrors(string source, string errorType);

        /// <summary>
        /// Зарегистрировать время выполнения парсинга
        /// </summary>
        void RecordParsingDuration(string source, long milliseconds);

        /// <summary>
        /// Получить текущие значения метрик
        /// </summary>
        MetricsSnapshot GetSnapshot();
    }

    /// <summary>
    /// Снимок метрик
    /// </summary>
    public class MetricsSnapshot
    {
        public int TotalProductsParsed { get; set; }
        public int TotalParserErrors { get; set; }
        public Dictionary<string, int> ProductsParsedBySource { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ErrorsByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, long> AverageParsingDurationBySource { get; set; } = new Dictionary<string, long>();
    }

    /// <summary>
    /// Простая реализация метрик для использования без Prometheus
    /// </summary>
    public class SimpleParserMetrics : IParserMetrics
    {
        private readonly ConcurrentDictionary<string, int> _productsParsed = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, int> _parserErrors = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, long> _parsingDurations = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, int> _parsingCount = new ConcurrentDictionary<string, int>();

        public void IncrementProductsParsed(string source, string category)
        {
            var key = $"{source}_{category}";
            _productsParsed.AddOrUpdate(key, 1, (_, count) => count + 1);
            
            // Также увеличиваем общий счетчик по источнику
            _productsParsed.AddOrUpdate(source, 1, (_, count) => count + 1);
        }

        public void IncrementParserErrors(string source, string errorType)
        {
            var key = $"{source}_{errorType}";
            _parserErrors.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        public void RecordParsingDuration(string source, long milliseconds)
        {
            _parsingDurations.AddOrUpdate(source, milliseconds, (_, total) => total + milliseconds);
            _parsingCount.AddOrUpdate(source, 1, (_, count) => count + 1);
        }

        public MetricsSnapshot GetSnapshot()
        {
            var snapshot = new MetricsSnapshot
            {
                TotalProductsParsed = _productsParsed.Values.Sum(),
                TotalParserErrors = _parserErrors.Values.Sum()
            };

            foreach (var kvp in _productsParsed)
            {
                if (!kvp.Key.Contains('_')) // Это источник, а не источник_категория
                {
                    snapshot.ProductsParsedBySource[kvp.Key] = kvp.Value;
                }
            }

            foreach (var kvp in _parserErrors)
            {
                var errorType = kvp.Key.Split('_').Last();
                snapshot.ErrorsByType[errorType] = kvp.Value;
            }

            foreach (var source in _parsingDurations.Keys)
            {
                if (_parsingCount.TryGetValue(source, out int count) && count > 0)
                {
                    snapshot.AverageParsingDurationBySource[source] = _parsingDurations[source] / count;
                }
            }

            return snapshot;
        }
    }

    /// <summary>
    /// Реализация метрик с использованием Prometheus (заглушка для реальной интеграции)
    /// </summary>
    public class PrometheusParserMetrics : IParserMetrics
    {
        // В реальной реализации здесь будут использоваться Counter, Histogram и т.д. из библиотеки prometheus-net
        // Пример:
        // private static readonly Counter ProductsParsedCounter = Metrics
        //     .CreateCounter("parser_products_parsed_total", "Number of parsed products", 
        //         new CounterConfiguration { LabelNames = new[] { "source", "category" } });

        public void IncrementProductsParsed(string source, string category)
        {
            // ProductsParsedCounter.WithLabels(source, category).Inc();
        }

        public void IncrementParserErrors(string source, string errorType)
        {
            // Аналогично для счетчика ошибок
        }

        public void RecordParsingDuration(string source, long milliseconds)
        {
            // Гистограмма для времени выполнения
        }

        public MetricsSnapshot GetSnapshot()
        {
            // В реальной реализации Prometheus сам собирает метрики
            return new MetricsSnapshot();
        }
    }
}