using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services.Caching;
using VseinstrumentiParser.Utilities;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Основной сервис для парсинга каталога электроинструментов с vseinstrumenti.ru с поддержкой кэширования
    /// </summary>
    public class VseinstrumentiParserServiceWithCache : IDisposable
    {
        private readonly HttpClientService _httpClient;
        private readonly ICategoryParser _categoryParser;
        private readonly IProductParser _productParser;
        private readonly ILogger _logger;
        private readonly RetryPolicy _retryPolicy;
        private readonly CacheService _cacheService;
        private readonly ParsingLimits _parsingLimits;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с внедрением зависимостей
        /// </summary>
        public VseinstrumentiParserServiceWithCache(
            ILogger? logger = null,
            CacheService? cacheService = null,
            ParsingLimits? parsingLimits = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _parsingLimits = parsingLimits ?? new ParsingLimits();
            
            _httpClient = new HttpClientService(_logger);
            _categoryParser = new CategoryParser(_httpClient, _logger);
            _productParser = new ProductParser(_httpClient, _logger);
            _retryPolicy = RetryPolicy.CreateForParsing(_logger);
            
            _cacheService = cacheService ?? new CacheService(_logger, _parsingLimits);
            
            _logger.Log($"Парсер инициализирован с кэшированием: {_parsingLimits.EnableCaching}");
        }

        /// <summary>
        /// Получить все категории электроинструментов (с кэшированием)
        /// </summary>
        public async Task<List<Category>> GetCategoriesAsync(bool useCache = true)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Начало получения категорий ===");
            
            if (useCache && _parsingLimits.EnableCaching)
            {
                var cacheKey = _cacheService.CreateCategoriesKey("vseinstrumenti");
                var cachedCategories = _cacheService.Get<List<Category>>(cacheKey);
                
                if (cachedCategories != null && cachedCategories.Any())
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Категории загружены из кэша: {cachedCategories.Count} категорий");
                    return cachedCategories;
                }
            }
            
            var categories = await _retryPolicy.ExecuteWithRetryAsync(async () =>
            {
                return await _categoryParser.GetCategoriesAsync();
            }, ex => ex.IsTransientNetworkError());
            
            if (useCache && _parsingLimits.EnableCaching && categories.Any())
            {
                var cacheKey = _cacheService.CreateCategoriesKey("vseinstrumenti");
                _cacheService.Set(cacheKey, categories);
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Категории сохранены в кэш: {categories.Count} категорий");
            }
            
            return categories;
        }

        /// <summary>
        /// Получить все товары из указанной категории (с кэшированием)
        /// </summary>
        /// <param name="categoryUrl">URL категории</param>
        /// <param name="maxPages">Максимальное количество страниц для парсинга</param>
        /// <param name="useCache">Использовать кэш</param>
        /// <returns>Список товаров</returns>
        public async Task<List<Product>> GetProductsFromCategoryAsync(
            string categoryUrl, 
            int maxPages = 5,
            bool useCache = true)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Парсинг товаров из категории: {categoryUrl} ===");
            
            if (useCache && _parsingLimits.EnableCaching)
            {
                var cacheKey = _cacheService.CreateProductsKey(categoryUrl, maxPages);
                var cachedProducts = _cacheService.Get<List<Product>>(cacheKey);
                
                if (cachedProducts != null && cachedProducts.Any())
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Товары загружены из кэша: {cachedProducts.Count} товаров");
                    return cachedProducts;
                }
            }
            
            // Получаем URL товаров
            var productUrls = await _retryPolicy.ExecuteWithRetryAsync(async () =>
            {
                return await _categoryParser.GetProductUrlsFromCategoryAsync(categoryUrl, maxPages);
            }, ex => ex.IsTransientNetworkError());
            
            if (productUrls.Count == 0)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] В категории не найдено товаров");
                return new List<Product>();
            }
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдено {productUrls.Count} товаров. Начинаем парсинг...");
            
            // Парсим товары
            var products = await _retryPolicy.ExecuteWithRetryAsync(async () =>
            {
                return await _productParser.ParseProductsAsync(productUrls, maxConcurrent: _parsingLimits.MaxConcurrentRequests);
            }, ex => ex.IsTransientNetworkError());
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Успешно распарсено {products.Count} товаров");
            
            if (useCache && _parsingLimits.EnableCaching && products.Any())
            {
                var cacheKey = _cacheService.CreateProductsKey(categoryUrl, maxPages);
                _cacheService.Set(cacheKey, products);
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Товары сохранены в кэш: {products.Count} товаров");
            }
            
            return products;
        }

        /// <summary>
        /// Получить все товары из всех категорий (с кэшированием)
        /// </summary>
        /// <param name="maxProductsPerCategory">Максимальное количество товаров на категорию</param>
        /// <param name="useCache">Использовать кэш</param>
        /// <returns>Словарь категория -> список товаров</returns>
        public async Task<Dictionary<Category, List<Product>>> GetAllProductsAsync(
            int maxProductsPerCategory = 20,
            bool useCache = true)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Начало полного парсинга каталога ===");
            
            var categories = await GetCategoriesAsync(useCache);
            var result = new Dictionary<Category, List<Product>>();
            
            int totalProducts = 0;
            int processedCategories = 0;
            
            foreach (var category in categories)
            {
                try
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Обработка категории: {category.Name} ({processedCategories + 1}/{categories.Count})");
                    
                    var products = await GetProductsFromCategoryAsync(category.Url, maxPages: 2, useCache);
                    
                    // Ограничиваем количество товаров
                    if (products.Count > maxProductsPerCategory)
                    {
                        products = products.Take(maxProductsPerCategory).ToList();
                    }
                    
                    result[category] = products;
                    totalProducts += products.Count;
                    processedCategories++;
                    
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Категория обработана: {products.Count} товаров");
                    
                    // Задержка между категориями для снижения нагрузки
                    if (processedCategories < categories.Count)
                    {
                        await Task.Delay(_parsingLimits.DelayBetweenRequestsMs);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при обработке категории {category.Name}: {ex.Message}");
                }
            }
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Полный парсинг завершен ===");
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Обработано категорий: {processedCategories}/{categories.Count}");
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Всего товаров: {totalProducts}");
            
            return result;
        }

        /// <summary>
        /// Очистить кэш
        /// </summary>
        public void ClearCache()
        {
            _cacheService.Clear();
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Кэш очищен");
        }

        /// <summary>
        /// Получить статистику кэша
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            return _cacheService.GetStatistics();
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _cacheService?.Dispose();
                _disposed = true;
            }
        }
    }
}