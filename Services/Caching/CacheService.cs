using System;
using System.Collections.Concurrent;
using System.Text.Json;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services.Caching
{
    /// <summary>
    /// Сервис кэширования результатов парсинга
    /// </summary>
    public class CacheService : IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache = new ConcurrentDictionary<string, CacheItem>();
        private readonly ILogger _logger;
        private readonly ParsingLimits _settings;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        /// <summary>
        /// Элемент кэша
        /// </summary>
        private class CacheItem
        {
            public object Data { get; set; } = null!;
            public DateTime Created { get; set; }
            public DateTime Expires { get; set; }
            public string Key { get; set; } = string.Empty;
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        public CacheService(ILogger logger, ParsingLimits settings)
        {
            _logger = logger;
            _settings = settings;

            // Запускаем таймер для очистки устаревших записей каждые 5 минут
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            
            _logger.Log($"Сервис кэширования инициализирован. Длительность кэша: {_settings.CacheDurationMinutes} минут");
        }

        /// <summary>
        /// Получить данные из кэша
        /// </summary>
        public T? Get<T>(string key)
        {
            if (!_settings.EnableCaching)
            {
                return default;
            }

            if (_cache.TryGetValue(key, out var item))
            {
                if (item.Expires > DateTime.Now)
                {
                    _logger.Log($"Кэш HIT для ключа: {key}");
                    return (T)item.Data;
                }
                else
                {
                    _logger.Log($"Кэш EXPIRED для ключа: {key}");
                    _cache.TryRemove(key, out _);
                }
            }
            else
            {
                _logger.Log($"Кэш MISS для ключа: {key}");
            }

            return default;
        }

        /// <summary>
        /// Сохранить данные в кэш
        /// </summary>
        public void Set<T>(string key, T data, TimeSpan? customDuration = null)
        {
            if (!_settings.EnableCaching)
            {
                return;
            }

            var duration = customDuration ?? TimeSpan.FromMinutes(_settings.CacheDurationMinutes);
            var item = new CacheItem
            {
                Data = data!,
                Created = DateTime.Now,
                Expires = DateTime.Now.Add(duration),
                Key = key
            };

            _cache[key] = item;
            _logger.Log($"Данные сохранены в кэш. Ключ: {key}, Срок действия: {duration.TotalMinutes} минут");
        }

        /// <summary>
        /// Создать ключ кэша для категорий
        /// </summary>
        public string CreateCategoriesKey(string site)
        {
            return $"categories_{site}_{DateTime.Now:yyyyMMdd}";
        }

        /// <summary>
        /// Создать ключ кэша для товаров категории
        /// </summary>
        public string CreateProductsKey(string categoryUrl, int maxPages)
        {
            var urlHash = Math.Abs(categoryUrl.GetHashCode());
            return $"products_{urlHash}_{maxPages}_{DateTime.Now:yyyyMMdd}";
        }

        /// <summary>
        /// Очистить кэш
        /// </summary>
        public void Clear()
        {
            var count = _cache.Count;
            _cache.Clear();
            _logger.Log($"Кэш очищен. Удалено записей: {count}");
        }

        /// <summary>
        /// Получить статистику кэша
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var now = DateTime.Now;
            var total = _cache.Count;
            var expired = 0;
            var valid = 0;

            foreach (var item in _cache.Values)
            {
                if (item.Expires > now)
                {
                    valid++;
                }
                else
                {
                    expired++;
                }
            }

            return new CacheStatistics
            {
                TotalItems = total,
                ValidItems = valid,
                ExpiredItems = expired,
                MemoryUsageMB = (double)_cache.Count * 0.1 // Примерная оценка
            };
        }

        /// <summary>
        /// Очистка устаревших записей
        /// </summary>
        private void CleanupExpiredItems(object? state)
        {
            try
            {
                var now = DateTime.Now;
                var removedCount = 0;

                foreach (var kvp in _cache)
                {
                    if (kvp.Value.Expires <= now)
                    {
                        if (_cache.TryRemove(kvp.Key, out _))
                        {
                            removedCount++;
                        }
                    }
                }

                if (removedCount > 0)
                {
                    _logger.Log($"Автоматическая очистка кэша: удалено {removedCount} устаревших записей");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при очистке кэша: {ex.Message}");
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Статистика кэша
    /// </summary>
    public class CacheStatistics
    {
        public int TotalItems { get; set; }
        public int ValidItems { get; set; }
        public int ExpiredItems { get; set; }
        public double MemoryUsageMB { get; set; }
    }
}