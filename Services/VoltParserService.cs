using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services.Metrics;
using VseinstrumentiParser.Utilities;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Основной сервис для парсинга каталога электроинструментов с 220-volt.ru
    /// </summary>
    public class VoltParserService : IDisposable
    {
        private readonly HttpClientService _httpClient;
        private readonly ICategoryParser _categoryParser;
        private readonly IProductParser _productParser;
        private readonly ILogger _logger;
        private readonly RetryPolicy _retryPolicy;
        private readonly IParserMetrics _metrics;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с внедрением зависимостей
        /// </summary>
        public VoltParserService(ILogger? logger = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _httpClient = new HttpClientService(_logger);
            _categoryParser = new VoltCategoryParser(_httpClient, _logger);
            _productParser = new VoltProductParser(_httpClient, _logger);
            _retryPolicy = RetryPolicy.CreateForParsing(_logger);
        }

        /// <summary>
        /// Конструктор с кастомными парсерами
        /// </summary>
        public VoltParserService(HttpClientService httpClient, ICategoryParser categoryParser, IProductParser productParser, ILogger? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _categoryParser = categoryParser ?? throw new ArgumentNullException(nameof(categoryParser));
            _productParser = productParser ?? throw new ArgumentNullException(nameof(productParser));
            _logger = logger ?? new ConsoleLogger();
            _retryPolicy = RetryPolicy.CreateForParsing(_logger);
        }

        /// <summary>
        /// Получить все категории электроинструментов
        /// </summary>
        public async Task<List<Category>> GetCategoriesAsync()
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Начало получения категорий с 220-volt.ru ===");
            
            return await _retryPolicy.ExecuteWithRetryAsync(async () =>
            {
                return await _categoryParser.GetCategoriesAsync("https://www.220-volt.ru");
            }, ex => ex.IsTransientNetworkError());
        }

        /// <summary>
        /// Получить все товары из указанной категории
        /// </summary>
        /// <param name="categoryUrl">URL категории</param>
        /// <param name="maxPages">Максимальное количество страниц для парсинга</param>
        /// <returns>Список товаров</returns>
        public async Task<List<Product>> GetProductsFromCategoryAsync(string categoryUrl, int maxPages = 5)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Парсинг товаров из категории: {categoryUrl} ===");
            
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
                return await _productParser.ParseProductsAsync(productUrls, maxConcurrent: 3);
            }, ex => ex.IsTransientNetworkError());
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Успешно распарсено {products.Count} товаров");
            return products;
        }

        /// <summary>
        /// Получить все товары из всех категорий
        /// </summary>
        /// <param name="maxProductsPerCategory">Максимальное количество товаров на категорию</param>
        /// <returns>Словарь категория -> список товаров</returns>
        public async Task<Dictionary<Category, List<Product>>> GetAllProductsAsync(int maxProductsPerCategory = 20)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Начало полного парсинга каталога 220-volt.ru ===");
            
            var categories = await GetCategoriesAsync();
            var result = new Dictionary<Category, List<Product>>();
            
            int totalProducts = 0;
            
            foreach (var category in categories)
            {
                try
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Обработка категории: {category.Name}");
                    
                    var products = await GetProductsFromCategoryAsync(category.Url, maxPages: 2);
                    
                    // Ограничиваем количество товаров
                    if (products.Count > maxProductsPerCategory)
                    {
                        products = products.Take(maxProductsPerCategory).ToList();
                    }
                    
                    result[category] = products;
                    totalProducts += products.Count;
                    
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Категория '{category.Name}': {products.Count} товаров");
                    
                    // Задержка между категориями
                    await Task.Delay(2500);
                }
                catch (Exception ex)
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при обработке категории '{category.Name}': {ex.Message}");
                    result[category] = new List<Product>();
                }
            }
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Полный парсинг завершен. Всего товаров: {totalProducts} ===");
            return result;
        }

        /// <summary>
        /// Получить детальную информацию о конкретном товаре
        /// </summary>
        public async Task<Product> GetProductDetailsAsync(string productUrl)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Получение деталей товара ===");
            
            return await _retryPolicy.ExecuteWithRetryAsync(async () =>
            {
                return await _productParser.ParseProductAsync(productUrl);
            }, ex => ex.IsTransientNetworkError());
        }

        /// <summary>
        /// Экспорт товаров в CSV файл
        /// </summary>
        public async Task ExportToCsvAsync(List<Product> products, string filePath)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Экспорт {products.Count} товаров в CSV ===");
            
            try
            {
                using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
                
                // Заголовок с дополнительными полями для характеристик инструментов
                await writer.WriteLineAsync("Название;Цена;Старая цена;Бренд;Артикул;Наличие;Мощность;Тип двигателя;Напряжение;Рейтинг;Отзывы;URL;Категория");
                
                // Данные
                foreach (var product in products)
                {
                    var line = $"\"{EscapeCsv(product.Name)}\";" +
                               $"{product.Price?.ToString("0.00") ?? "0"};" +
                               $"{product.OldPrice?.ToString("0.00") ?? ""};" +
                               $"\"{EscapeCsv(product.Brand)}\";" +
                               $"\"{EscapeCsv(product.Article)}\";" +
                               $"\"{EscapeCsv(product.Availability.ToString())}\";" +
                               $"\"{EscapeCsv(product.Specifications.GetValueOrDefault("Мощность", ""))}\";" +
                               $"\"{EscapeCsv(product.Specifications.GetValueOrDefault("Тип двигателя", ""))}\";" +
                               $"\"{EscapeCsv(product.Specifications.GetValueOrDefault("Напряжение", ""))}\";" +
                               $"{product.Rating?.ToString("0.0") ?? ""};" +
                               $"{product.ReviewCount?.ToString() ?? ""};" +
                               $"\"{EscapeCsv(product.Url)}\";" +
                               $"\"{EscapeCsv(product.Category)}\"";
                    
                    await writer.WriteLineAsync(line);
                }
                
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Экспорт завершен: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при экспорте в CSV: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Экспорт категорий в CSV файл
        /// </summary>
        public async Task ExportCategoriesToCsvAsync(List<Category> categories, string filePath)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] === Экспорт {categories.Count} категорий в CSV ===");
            
            try
            {
                using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
                
                await writer.WriteLineAsync("Название;URL;Количество товаров;Подкатегории");
                
                foreach (var category in categories)
                {
                    var subCategoriesCount = category.SubCategories.Count;
                    var line = $"\"{EscapeCsv(category.Name)}\";" +
                               $"\"{EscapeCsv(category.Url)}\";" +
                               $"{category.ProductCount?.ToString() ?? "0"};" +
                               $"{subCategoriesCount}";
                    
                    await writer.WriteLineAsync(line);
                }
                
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Экспорт категорий завершен: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при экспорте категорий: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Экранирование строк для CSV
        /// </summary>
        private string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\"", "\"\"");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}