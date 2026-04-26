using AngleSharp;
using AngleSharp.Dom;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Реализация парсера категорий для сайта vseinstrumenti.ru
    /// Использует композицию: IHtmlLoader для загрузки HTML
    /// </summary>
    public class CategoryParser : ICategoryParser
    {
        private readonly IHtmlLoader _htmlLoader;
        private readonly ILogger _logger;
        private readonly IBrowsingContext _browsingContext;

        public CategoryParser(IHtmlLoader htmlLoader, ILogger? logger = null)
        {
            _htmlLoader = htmlLoader ?? throw new ArgumentNullException(nameof(htmlLoader));
            _logger = logger ?? new ConsoleLogger();
            
            // Настройка AngleSharp
            var config = Configuration.Default.WithDefaultLoader();
            _browsingContext = BrowsingContext.New(config);
        }

        /// <inheritdoc />
        public async Task<List<Category>> GetCategoriesAsync(string baseUrl = "https://www.vseinstrumenti.ru")
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Начало парсинга категорий с {baseUrl}");
            
            try
            {
                // Получаем главную страницу для получения кук
                await _htmlLoader.LoadHtmlAsync(baseUrl);
                
                // Получаем страницу категорий электроинструментов
                string url = $"{baseUrl}/category/elektroinstrumenty/";
                string html = await _htmlLoader.LoadHtmlAsync(url);
                
                var document = await _browsingContext.OpenAsync(req => req.Content(html));
                
                // Ищем блоки категорий
                var categoryElements = document.QuerySelectorAll(".category-block, .catalog-section, .subcategory-item");
                
                var categories = new List<Category>();
                
                foreach (var element in categoryElements)
                {
                    var link = element.QuerySelector("a");
                    if (link == null) continue;
                    
                    var category = new Category
                    {
                        Name = link.TextContent?.Trim() ?? "Без названия",
                        Url = MakeAbsoluteUrl(link.GetAttribute("href") ?? "", baseUrl)
                    };
                    
                    // Пытаемся получить количество товаров
                    var countElement = element.QuerySelector(".count, .product-count");
                    if (countElement != null)
                    {
                        var countText = countElement.TextContent?.Trim();
                        if (int.TryParse(countText?.Replace("(", "").Replace(")", "").Replace("товаров", "").Trim(), out int count))
                        {
                            category.ProductCount = count;
                        }
                    }
                    
                    categories.Add(category);
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдена категория: {category.Name}");
                }
                
                // Если не нашли через стандартные селекторы, используем альтернативные
                if (categories.Count == 0)
                {
                    categories = await FallbackCategoryParsing(document, baseUrl);
                }
                
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
                string html = await _htmlLoader.LoadHtmlAsync(categoryUrl);
                var document = await _browsingContext.OpenAsync(req => req.Content(html));
                
                var subCategories = new List<Category>();
                
                // Ищем подкатегории в левом меню или в основном контенте
                var subCategoryElements = document.QuerySelectorAll(".subcategory-list a, .filter-section a, .catalog-subcategory a");
                
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
                    
                    string html = await _htmlLoader.LoadHtmlAsync(pageUrl);
                    var document = await _browsingContext.OpenAsync(req => req.Content(html));
                    
                    // Ищем ссылки на товары
                    var productLinks = document.QuerySelectorAll(".product-card a, .catalog-item a, .item-title a");
                    
                    bool foundNew = false;
                    foreach (var link in productLinks)
                    {
                        var href = link.GetAttribute("href");
                        if (!string.IsNullOrEmpty(href) && href.Contains("/product/"))
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
                    var nextPage = document.QuerySelector(".pagination-next, .next-page, a[rel='next']");
                    if (nextPage == null || !foundNew)
                    {
                        break;
                    }
                    
                    page++;
                    
                    // Задержка между запросами
                    await Task.Delay(1000);
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
            var navItems = document.QuerySelectorAll(".main-nav a, .top-menu a");
            
            foreach (var item in navItems)
            {
                var href = item.GetAttribute("href");
                var text = item.TextContent?.Trim();
                
                if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(text) &&
                    (href.Contains("elektroinstrument") || href.Contains("instrument")))
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