using AngleSharp;
using AngleSharp.Dom;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Реализация парсера категорий для сайта 220-volt.ru
    /// </summary>
    public class VoltCategoryParser : ICategoryParser
    {
        private readonly HttpClientService _httpClient;
        private readonly ILogger _logger;
        private readonly IBrowsingContext _browsingContext;

        public VoltCategoryParser(HttpClientService httpClient, ILogger? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? new ConsoleLogger();
            
            // Настройка AngleSharp
            var config = Configuration.Default.WithDefaultLoader();
            _browsingContext = BrowsingContext.New(config);
        }

        /// <inheritdoc />
        public async Task<List<Category>> GetCategoriesAsync(string baseUrl = "https://www.220-volt.ru")
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Начало парсинга категорий с {baseUrl}");
            
            try
            {
                // Получаем главную страницу для получения кук
                await _httpClient.GetHtmlAsync(baseUrl);
                
                // Получаем страницу категорий электроинструментов
                string url = $"{baseUrl}/catalog-9889-elektroinstrumenty/";
                string html = await _httpClient.GetHtmlAsync(url);
                
                var document = await _browsingContext.OpenAsync(req => req.Content(html));
                
                // Ищем блоки категорий (типичная структура 220-volt)
                var categoryElements = document.QuerySelectorAll(".catalog-section, .category-item, .subcategory-list a, .catalog-category");
                
                var categories = new List<Category>();
                
                // Если не нашли через стандартные селекторы, ищем в навигации
                if (categoryElements.Length == 0)
                {
                    categoryElements = document.QuerySelectorAll(".left-menu a, .sidebar-nav a, .menu-catalog a");
                }
                
                foreach (var element in categoryElements)
                {
                    var link = element.QuerySelector("a") ?? element;
                    var href = link.GetAttribute("href");
                    if (string.IsNullOrEmpty(href) || !href.Contains("/catalog-")) continue;
                    
                    var category = new Category
                    {
                        Name = link.TextContent?.Trim() ?? "Без названия",
                        Url = MakeAbsoluteUrl(href, baseUrl)
                    };
                    
                    // Пытаемся получить количество товаров
                    var countElement = element.QuerySelector(".count, .product-count, .quantity");
                    if (countElement != null)
                    {
                        var countText = countElement.TextContent?.Trim();
                        if (int.TryParse(countText?.Replace("(", "").Replace(")", "").Replace("товаров", "").Replace("товара", "").Trim(), out int count))
                        {
                            category.ProductCount = count;
                        }
                    }
                    
                    // Проверяем, что это категория электроинструментов
                    if (category.Name.Contains("Дрель") || category.Name.Contains("Перфоратор") || 
                        category.Name.Contains("Шлифмашина") || category.Name.Contains("Пила") ||
                        category.Name.Contains("Электроинструмент") || href.Contains("elektroinstrument"))
                    {
                        categories.Add(category);
                        _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдена категория: {category.Name}");
                    }
                }
                
                // Если всё ещё не нашли, используем альтернативный метод
                if (categories.Count == 0)
                {
                    categories = await FallbackCategoryParsing(document, baseUrl);
                }
                
                // Убираем дубликаты
                categories = categories
                    .GroupBy(c => c.Url)
                    .Select(g => g.First())
                    .ToList();
                
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдено {categories.Count} категорий");
                return categories;
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при парсинге категорий: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Category>> GetSubCategoriesAsync(string categoryUrl)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Парсинг подкатегорий с {categoryUrl}");
            
            try
            {
                string html = await _httpClient.GetHtmlAsync(categoryUrl);
                var document = await _browsingContext.OpenAsync(req => req.Content(html));
                
                var subCategories = new List<Category>();
                
                // Ищем подкатегории в основном контенте
                var subCategoryElements = document.QuerySelectorAll(".subcategory-list a, .filter-section a, .catalog-subcategory a, .category-child a");
                
                foreach (var element in subCategoryElements)
                {
                    var urlAttr = element.GetAttribute("href");
                    if (string.IsNullOrEmpty(urlAttr) || urlAttr.Contains("#")) continue;
                    
                    var category = new Category
                    {
                        Name = element.TextContent?.Trim() ?? "Без названия",
                        Url = MakeAbsoluteUrl(urlAttr, categoryUrl)
                    };
                    
                    subCategories.Add(category);
                }
                
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдено {subCategories.Count} подкатегорий");
                return subCategories;
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при парсинге подкатегорий: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetProductUrlsFromCategoryAsync(string categoryUrl, int maxPages = 10)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Поиск товаров в категории: {categoryUrl}");
            
            var productUrls = new List<string>();
            int page = 1;
            
            try
            {
                while (page <= maxPages)
                {
                    string pageUrl = page == 1 ? categoryUrl : $"{categoryUrl}?page={page}";
                    
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Парсинг страницы {page}: {pageUrl}");
                    
                    string html = await _httpClient.GetHtmlAsync(pageUrl);
                    var document = await _browsingContext.OpenAsync(req => req.Content(html));
                    
                    // Ищем ссылки на товары (типичные селекторы 220-volt)
                    var productLinks = document.QuerySelectorAll(".product-card a, .catalog-item a, .item-title a, .product-name a, .goods-item a");
                    
                    bool foundNew = false;
                    foreach (var link in productLinks)
                    {
                        var href = link.GetAttribute("href");
                        if (!string.IsNullOrEmpty(href) && (href.Contains("/product/") || href.Contains("/goods/")))
                        {
                            var fullUrl = MakeAbsoluteUrl(href, categoryUrl);
                            if (!productUrls.Contains(fullUrl))
                            {
                                productUrls.Add(fullUrl);
                                foundNew = true;
                            }
                        }
                    }
                    
                    // Проверяем наличие следующей страницы
                    var nextPage = document.QuerySelector(".pagination-next, .next-page, a[rel='next'], .page-next");
                    if (nextPage == null || !foundNew)
                    {
                        // Также проверяем, есть ли кнопка "Показать еще" (динамическая подгрузка)
                        var showMore = document.QuerySelector(".show-more, .load-more, .js-load-more");
                        if (showMore == null)
                        {
                            break;
                        }
                    }
                    
                    page++;
                    
                    // Задержка между запросами
                    await Task.Delay(1500);
                }
                
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдено {productUrls.Count} товаров в категории");
                return productUrls;
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при поиске товаров: {ex.Message}");
                return productUrls;
            }
        }

        /// <summary>
        /// Альтернативный метод парсинга категорий
        /// </summary>
        private async Task<List<Category>> FallbackCategoryParsing(IDocument document, string baseUrl)
        {
            var categories = new List<Category>();
            
            // Парсим навигационное меню
            var navItems = document.QuerySelectorAll(".main-nav a, .top-menu a, .header-menu a");
            
            foreach (var item in navItems)
            {
                var href = item.GetAttribute("href");
                var text = item.TextContent?.Trim();
                
                if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(text) &&
                    (href.Contains("/catalog-") || href.Contains("/category/")))
                {
                    categories.Add(new Category
                    {
                        Name = text,
                        Url = MakeAbsoluteUrl(href, baseUrl)
                    });
                }
            }
            
            return categories;
        }

        /// <summary>
        /// Преобразует относительный URL в абсолютный
        /// </summary>
        private string MakeAbsoluteUrl(string url, string baseUrl)
        {
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return url;
            }
            
            if (url.StartsWith("//"))
            {
                return "https:" + url;
            }
            
            if (url.StartsWith("/"))
            {
                var uri = new Uri(baseUrl);
                return $"{uri.Scheme}://{uri.Host}{url}";
            }
            
            return baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }
    }
}