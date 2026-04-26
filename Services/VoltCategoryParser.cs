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
                
                var categories = new List<Category>();
                
                // Приоритетные селекторы для категорий (требуют проверки на реальном сайте)
                var categorySelectors = new[]
                {
                    ".catalog-section-item",
                    ".category-card",
                    ".catalog-category-item",
                    ".subcategory-list a",
                    ".catalog-item",
                    ".category-link",
                };

                foreach (var selector in categorySelectors)
                {
                    var categoryElements = document.QuerySelectorAll(selector);
                    if (categoryElements.Length > 0)
                    {
                        _logger.Log($"[{DateTime.Now:HH:mm:ss}] Используется селектор категорий: {selector} ({categoryElements.Length} элементов)");
                        
                        foreach (var element in categoryElements)
                        {
                            var category = ExtractCategoryFromElement(element, baseUrl);
                            if (category != null && IsElectrotoolCategory(category.Name))
                            {
                                categories.Add(category);
                                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Найдена категория: {category.Name}");
                            }
                        }
                        
                        if (categories.Count > 0) break;
                    }
                }
                    
                // Если не нашли через стандартные селекторы, ищем в навигации
                if (categories.Count == 0)
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Стандартные селекторы не нашли категории, пробуем навигацию...");
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
            
        /// <summary>
        /// Извлечение категории из элемента
        /// </summary>
        private Category ExtractCategoryFromElement(IElement element, string baseUrl)
        {
            var link = element.QuerySelector("a") ?? element;
            var href = link.GetAttribute("href");
            
            if (string.IsNullOrEmpty(href) || !href.Contains("/catalog-")) 
            {
                return null;
            }
            
            var category = new Category
            {
                Name = CleanText(link.TextContent),
                Url = MakeAbsoluteUrl(href, baseUrl)
            };
            
            // Пытаемся получить количество товаров
            var countSelectors = new[] { ".count", ".product-count", ".quantity", "span", ".item-count" };
            foreach (var selector in countSelectors)
            {
                var countElement = element.QuerySelector(selector);
                if (countElement != null)
                {
                    var countText = CleanText(countElement.TextContent);
                    if (TryParseCategoryCount(countText, out int count))
                    {
                        category.ProductCount = count;
                        break;
                    }
                }
            }
            
            return category;
        }

        /// <summary>
        /// Проверка, что категория относится к электроинструментам
        /// </summary>
        private bool IsElectrotoolCategory(string categoryName)
        {
            var keywords = new[]
            {
                "дрель", "перфоратор", "шлифмашина", "пила", "электроинструмент",
                "шуруповерт", "болгарка", "степлер", "ножовка", "лобзик",
                "фрезер", "рубанок", "миксер", "помпа", "генератор"
            };
            
            var lowerName = categoryName.ToLowerInvariant();
            return keywords.Any(keyword => lowerName.Contains(keyword));
        }

        /// <summary>
        /// Очистка текста
        /// </summary>
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "Без названия";
            return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Парсинг количества товаров из строки
        /// </summary>
        private bool TryParseCategoryCount(string countText, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(countText)) return false;
            
            // Удаляем всё, кроме цифр
            var cleanText = System.Text.RegularExpressions.Regex.Replace(countText, @"\D", "");
            return int.TryParse(cleanText, out count);
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
                    
                    // Приоритетные селекторы для карточек товаров (требуют проверки)
                    var productCardSelectors = new[]
                    {
                        ".product-card",
                        ".goods-item",
                        ".catalog-item",
                        ".product-item",
                        ".item",
                        ".goods-card",
                        ".catalog-product",
                    };

                    bool foundNew = false;
                    
                    foreach (var cardSelector in productCardSelectors)
                    {
                        var productCards = document.QuerySelectorAll(cardSelector);
                        
                        foreach (var card in productCards)
                        {
                            // Ищем ссылку внутри карточки
                            var linkSelectors = new[]
                            {
                                "a[href*='/catalog-']",
                                "a[href*='/product/']",
                                "a[href*='/goods/']",
                                "a.title",
                                "a.product-link",
                                ".item-title a",
                            };

                            foreach (var linkSelector in linkSelectors)
                            {
                                var link = card.QuerySelector(linkSelector);
                                if (link != null)
                                {
                                    var href = link.GetAttribute("href");
                                    if (!string.IsNullOrEmpty(href))
                                    {
                                        var fullUrl = MakeAbsoluteUrl(href, categoryUrl);
                                        if (!productUrls.Contains(fullUrl))
                                        {
                                            productUrls.Add(fullUrl);
                                            foundNew = true;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (productCards.Length > 0) break;
                    }
                    
                    // Если не нашли карточки, пробуем искать все ссылки на товары
                    if (productUrls.Count == 0 || !foundNew)
                    {
                        var directLinks = document.QuerySelectorAll("a[href*='/catalog-1'], a[href*='/product/'], a[href*='/goods/']");
                        foreach (var link in directLinks)
                        {
                            var href = link.GetAttribute("href");
                            if (!string.IsNullOrEmpty(href))
                            {
                                var fullUrl = MakeAbsoluteUrl(href, categoryUrl);
                                if (!productUrls.Contains(fullUrl))
                                {
                                    productUrls.Add(fullUrl);
                                    foundNew = true;
                                }
                            }
                        }
                    }
                    
                    // Проверяем наличие следующей страницы
                    var nextPageSelectors = new[]
                    {
                        ".pagination-next",
                        ".next-page",
                        "a[rel='next']",
                        ".page-next",
                        ".pagination a:last-child",
                        ".load-more",
                        ".show-more",
                    };

                    var nextPage = document.QuerySelector(string.Join(", ", nextPageSelectors));
                    
                    if (nextPage == null || !foundNew)
                    {
                        _logger.Log($"[{DateTime.Now:HH:mm:ss}] Следующая страница не найдена или новых товаров нет");
                        break;
                    }
                    
                    page++;
                    
                    // Задержка между запросами (уважение к роботу сайта)
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