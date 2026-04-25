using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Utilities
{
    /// <summary>
    /// Политика повторных попыток с экспоненциальной задержкой
    /// </summary>
    public class RetryPolicy
    {
        private readonly ILogger _logger;
        private readonly int _maxRetries;
        private readonly int _initialDelayMs;
        private readonly double _backoffMultiplier;
        private readonly int _maxDelayMs;

        public RetryPolicy(ILogger? logger = null, int maxRetries = 3, int initialDelayMs = 1000, 
                          double backoffMultiplier = 2.0, int maxDelayMs = 30000)
        {
            _logger = logger ?? new ConsoleLogger();
            _maxRetries = maxRetries;
            _initialDelayMs = initialDelayMs;
            _backoffMultiplier = backoffMultiplier;
            _maxDelayMs = maxDelayMs;
        }

        /// <summary>
        /// Выполнить операцию с повторными попытками
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения</typeparam>
        /// <param name="operation">Асинхронная операция</param>
        /// <param name="shouldRetry">Функция, определяющая, нужно ли повторять при ошибке</param>
        /// <returns>Результат операции</returns>
        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, Func<Exception, bool>? shouldRetry = null)
        {
            int attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetries)
            {
                try
                {
                    if (attempt > 0)
                    {
                        _logger.Log($"[{DateTime.Now:HH:mm:ss}] Повторная попытка {attempt}/{_maxRetries}");
                    }
                    
                    return await operation();
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    lastException = ex;
                    
                    if (shouldRetry != null && !shouldRetry(ex))
                    {
                        _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка не требует повторной попытки: {ex.Message}");
                        throw;
                    }
                    
                    attempt++;
                    int delay = CalculateDelay(attempt);
                    
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка: {ex.Message}. Повтор через {delay}мс");
                    await Task.Delay(delay);
                }
            }
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Превышено максимальное количество попыток ({_maxRetries})");
            throw lastException ?? new InvalidOperationException("Неизвестная ошибка при выполнении операции");
        }

        /// <summary>
        /// Выполнить операцию без возвращаемого значения
        /// </summary>
        public async Task ExecuteWithRetryAsync(Func<Task> operation, Func<Exception, bool>? shouldRetry = null)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await operation();
                return true;
            }, shouldRetry);
        }

        /// <summary>
        /// Рассчитать задержку для текущей попытки
        /// </summary>
        private int CalculateDelay(int attempt)
        {
            double delay = _initialDelayMs * Math.Pow(_backoffMultiplier, attempt - 1);
            delay = Math.Min(delay, _maxDelayMs);
            return (int)delay;
        }

        /// <summary>
        /// Стандартная политика для HTTP-запросов
        /// </summary>
        public static RetryPolicy CreateForHttpRequests(ILogger? logger = null)
        {
            return new RetryPolicy(
                logger: logger,
                maxRetries: 3,
                initialDelayMs: 2000,
                backoffMultiplier: 2.0,
                maxDelayMs: 15000
            );
        }

        /// <summary>
        /// Политика для парсинга с большим количеством попыток
        /// </summary>
        public static RetryPolicy CreateForParsing(ILogger? logger = null)
        {
            return new RetryPolicy(
                logger: logger,
                maxRetries: 5,
                initialDelayMs: 1000,
                backoffMultiplier: 1.5,
                maxDelayMs: 30000
            );
        }
    }

    /// <summary>
    /// Расширения для работы с исключениями
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Проверить, является ли исключение временной ошибкой сети
        /// </summary>
        public static bool IsTransientNetworkError(this Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   (ex.InnerException != null && IsTransientNetworkError(ex.InnerException));
        }

        /// <summary>
        /// Проверить, является ли исключение ошибкой "слишком много запросов"
        /// </summary>
        public static bool IsRateLimitError(this Exception ex)
        {
            var message = ex.Message.ToLower();
            return message.Contains("429") ||
                   message.Contains("too many requests") ||
                   message.Contains("rate limit") ||
                   message.Contains("превышен лимит");
        }
    }
}