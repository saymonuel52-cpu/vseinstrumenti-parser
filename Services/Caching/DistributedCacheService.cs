using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services.Caching
{
    /// <summary>
    /// Сервис распределенного кэширования с поддержкой IDistributedCache
    /// </summary>
    public class DistributedCacheService : IDisposable
    {
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger _logger;
        private readonly ParsingLimits _settings;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с IDistributedCache
        /// </summary>
        public DistributedCacheService(
            IDistributedCache distributedCache,
            ILogger logger,
            ParsingLimits settings)
        {
            _distributedCache = distributedCache;
            _logger = logger;
            _settings = settings;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
            
            _logger.LogInformation($"DistributedCacheService инициализирован. Кэширование: {_settings.EnableCaching}");
        }

        /// <summary>
        /// Получить данные из распределенного кэша
        /// </summary>
        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (!_settings.EnableCaching)
            {
                return default;
            }

            try
            {
                var cachedData = await _distributedCache.GetAsync(key, cancellationToken);
                if (cachedData == null || cachedData.Length == 0)
                {
                    _logger.LogDebug($"Кэш MISS для ключа: {key}");
                    return default;
                }

                var json = Encoding.UTF8.GetString(cachedData);
                var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                
                _logger.LogDebug($"Кэш HIT для ключа: {key}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Ошибка при чтении из распределенного кэша (ключ: {key}): {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Сохранить данные в распределенный кэш
        /// </summary>
        public async Task SetAsync<T>(
            string key, 
            T data, 
            TimeSpan? absoluteExpiration = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.EnableCaching || data == null)
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                var options = new DistributedCacheEntryOptions();
                
                // Устанавливаем абсолютное время жизни
                if (absoluteExpiration.HasValue)
                {
                    options.SetAbsoluteExpiration(absoluteExpiration.Value);
                }
                else
                {
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
                }
                
                // Устанавливаем скользящее время жизни
                if (slidingExpiration.HasValue)
                {
                    options.SetSlidingExpiration(slidingExpiration.Value);
                }
                
                await _distributedCache.SetAsync(key, bytes, options, cancellationToken);
                
                _logger.LogDebug($"Данные сохранены в распределенный кэш. Ключ: {key}, Размер: {bytes.Length} байт");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Ошибка при записи в распределенный кэш (ключ: {key}): {ex.Message}");
            }
        }

        /// <summary>
        /// Удалить данные из кэша
        /// </summary>
        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                _logger.LogDebug($"Данные удалены из распределенного кэша. Ключ: {key}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Ошибка при удалении из распределенного кэша (ключ: {key}): {ex.Message}");
            }
        }

        /// <summary>
        /// Обновить время жизни данных в кэше
        /// </summary>
        public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _distributedCache.RefreshAsync(key, cancellationToken);
                _logger.LogDebug($"Время жизни обновлено для ключа: {key}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Ошибка при обновлении времени жизни (ключ: {key}): {ex.Message}");
            }
        }

        /// <summary>
        /// Получить данные с возможностью обновления через фабрику
        /// </summary>
        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? absoluteExpiration = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.EnableCaching)
            {
                return await factory();
            }

            // Пытаемся получить из кэша
            var cachedValue = await GetAsync<T>(key, cancellationToken);
            if (cachedValue != null && !IsDefaultValue(cachedValue))
            {
                return cachedValue;
            }

            // Если нет в кэше, вызываем фабрику
            _logger.LogDebug($"Данные не найдены в кэше, выполнение фабрики для ключа: {key}");
            var value = await factory();
            
            // Сохраняем в кэш
            if (value != null && !IsDefaultValue(value))
            {
                await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);
            }
            
            return value;
        }

        /// <summary>
        /// Создать ключ кэша для категорий
        /// </summary>
        public string CreateCategoriesKey(string site)
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            return $"categories:{site}:{today}";
        }

        /// <summary>
        /// Создать ключ кэша для товаров категории
        /// </summary>
        public string CreateProductsKey(string categoryUrl, int maxPages)
        {
            var urlHash = Math.Abs(categoryUrl.GetHashCode());
            var today = DateTime.Now.ToString("yyyyMMdd");
            return $"products:{urlHash}:{maxPages}:{today}";
        }

        /// <summary>
        /// Создать ключ кэша с версией для инвалидации
        /// </summary>
        public string CreateVersionedKey(string baseKey, string version = "v1")
        {
            return $"{baseKey}:{version}";
        }

        /// <summary>
        /// Инвалидировать кэш по паттерну
        /// </summary>
        public async Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // Внимание: Эта реализация зависит от бэкенда кэша
            // Для Redis можно использовать SCAN + DEL
            // Для SQL Server можно использовать поиск по префиксу
            _logger.LogInformation($"Запрошена инвалидация кэша по паттерну: {pattern}");
            
            // В реальной реализации здесь будет логика поиска и удаления ключей по паттерну
            // Для примера просто логируем
            _logger.LogWarning($"Инвалидация по паттерну не реализована для текущего бэкенда кэша");
        }

        /// <summary>
        /// Получить информацию о кэше
        /// </summary>
        public async Task<DistributedCacheInfo> GetCacheInfoAsync(CancellationToken cancellationToken = default)
        {
            // Внимание: Эта реализация зависит от бэкенда кэша
            // Для Redis можно использовать INFO command
            // Для SQL Server можно запросить статистику из таблицы
            
            return new DistributedCacheInfo
            {
                CacheType = GetCacheType(),
                IsEnabled = _settings.EnableCaching,
                DefaultExpirationMinutes = _settings.CacheDurationMinutes
            };
        }

        /// <summary>
        /// Определить тип бэкенда кэша
        /// </summary>
        private string GetCacheType()
        {
            var type = _distributedCache.GetType().Name;
            
            if (type.Contains("Redis", StringComparison.OrdinalIgnoreCase))
                return "Redis";
            if (type.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                return "SQL Server";
            if (type.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                return "In-Memory";
            
            return type;
        }

        /// <summary>
        /// Проверить, является ли значение значением по умолчанию
        /// </summary>
        private bool IsDefaultValue<T>(T value)
        {
            return EqualityComparer<T>.Default.Equals(value, default);
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // IDistributedCache обычно не требует явного освобождения
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Информация о распределенном кэше
    /// </summary>
    public class DistributedCacheInfo
    {
        public string CacheType { get; set; } = "Unknown";
        public bool IsEnabled { get; set; }
        public int DefaultExpirationMinutes { get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
    }

    /// <summary>
    /// Расширения для регистрации в DI
    /// </summary>
    public static class DistributedCacheExtensions
    {
        /// <summary>
        /// Добавить распределенное кэширование с Redis
        /// </summary>
        public static IServiceCollection AddRedisDistributedCache(
            this IServiceCollection services,
            string connectionString,
            string instanceName = "ParserCache")
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = instanceName;
            });
            
            services.AddSingleton<DistributedCacheService>();
            
            return services;
        }

        /// <summary>
        /// Добавить распределенное кэширование с SQL Server
        /// </summary>
        public static IServiceCollection AddSqlServerDistributedCache(
            this IServiceCollection services,
            string connectionString,
            string schemaName = "dbo",
            string tableName = "Cache")
        {
            services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = connectionString;
                options.SchemaName = schemaName;
                options.TableName = tableName;
            });
            
            services.AddSingleton<DistributedCacheService>();
            
            return services;
        }

        /// <summary>
        /// Добавить in-memory распределенное кэширование (для разработки)
        /// </summary>
        public static IServiceCollection AddMemoryDistributedCache(
            this IServiceCollection services)
        {
            services.AddDistributedMemoryCache();
            services.AddSingleton<DistributedCacheService>();
            
            return services;
        }
    }
}