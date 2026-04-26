using AngleSharp;
using AngleSharp.Dom;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Реализация парсера товаров для сайта 220-volt.ru
    /// Использует композицию: IHtmlLoader для загрузки, DataSanitizer для очистки данных
    /// </summary>
    public class VoltProductParser : IProductParser
    {
        private readonly IHtmlLoader _htmlLoader;
        private readonly DataSanitizer _sanitizer;
        private readonly ILogger _logger;
        private readonly IBrowsingContext _browsingContext;

        public VoltProductParser(IHtmlLoader htmlLoader, DataSanitizer sanitizer, ILogger? logger = null)
        {
            _htmlLoader = htmlLoader ?? throw new ArgumentNullException(nameof(htmlLoader));
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
            _logger = logger ?? new ConsoleLogger();
            
            var config = Configuration.Default.WithDefaultLoader();
            _browsingContext = BrowsingContext.New(config);
        }

        /// <inheritdoc />
        public async Task<Product> ParseProductAsync(string productUrl)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Парсинг товара: {productUrl}");
            
            try
            {
                string html = await _htmlLoader.LoadHtmlAsync(productUrl);
                var document = await _browsingContext.OpenAsync(req => req.Content(html));
                
                var product = new Product
                {
                    Url = productUrl,
                    ParsedAt = DateTime.UtcNow
                };
                
                // Извлекаем название товара
                product.Name = ExtractProductName(document);
                
                // Извлекаем цену
                ExtractPrice(document, product);
                
                // Извлекаем бренд
                product.Brand = ExtractBrand(document);
                
                // Извлекаем артикул
                product.Article = ExtractArticle(document);
                
                // Извлекаем наличие
                ExtractAvailability(document, product);
                
                // Извлекаем характеристики
                product.Specifications = ExtractSpecifications(document);
                
                // Извлекаем рейтинг и отзывы
                ExtractRating(document, product);
                
                // Извлекаем категорию
                product.Category = ExtractCategory(document);
                
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Успешно распарсен товар: {product.Name}");
                return product;
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при парсинге товара {productUrl}: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Product>> ParseProductsAsync(IEnumerable<string> productUrls, int maxConcurrent = 5)
        {
            var allProducts = new List<Product>();
            var semaphore = new SemaphoreSlim(maxConcurrent);
            var tasks = new List<Task>();
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Начало пакетного парсинга {productUrls.Count()} товаров");
            
            foreach (var url in productUrls)
            {
                await semaphore.WaitAsync();
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var product = await ParseProductAsync(url);
                        lock (allProducts)
                        {
                            allProducts.Add(product);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка при парсинге {url}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    
                    // Задержка между запросами
                    await Task.Delay(800);
                }));
            }
            
            await Task.WhenAll(tasks);
            
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] Пакетный парсинг завершен. Успешно: {allProducts.Count}");
            return allProducts;
        }

        /// <summary>
        /// Извлечение названия товара
        /// </summary>
        private string ExtractProductName(IDocument document)
        {
            // Приоритетные селекторы для 220-volt.ru (требуют проверки на реальном сайте)
            var selectors = new[]
            {
                "h1.product-title",
                "h1[itemprop='name']",
                ".goods-header h1",
                "h1.product-name",
                ".item-title h1",
                ".product-name h1",
                "h1", // Fallback - первый h1
            };

            foreach (var selector in selectors)
            {
                var nameElement = document.QuerySelector(selector);
                if (nameElement != null)
                {
                    var text = nameElement.TextContent?.Trim();
                    if (!string.IsNullOrEmpty(text) && text.Length > 2)
                    {
                        // Очистка через DataSanitizer
                        return _sanitizer.CleanProductName(text);
                    }
                }
            }
            
            return "Неизвестно";
        }

        /// <summary>
        /// Извлечение цены
        /// </summary>
        private void ExtractPrice(IDocument document, Product product)
        {
            // Приоритетные селекторы для текущей цены
            var priceSelectors = new[]
            {
                ".price-current",
                ".product-price",
                ".goods-price",
                "[itemprop='price']",
                ".price-block",
                ".current-price",
                ".price",
                ".product-cost",
            };

            foreach (var selector in priceSelectors)
            {
                var priceElement = document.QuerySelector(selector);
                if (priceElement != null)
                {
                    var priceText = priceElement.TextContent?.Trim();
                    // Если это элемент с microdata, пробуем взять из content атрибута
                    if (priceElement.HasAttribute("content"))
                    {
                        var contentPrice = priceElement.GetAttribute("content");
                        if (_sanitizer.TryParsePrice(contentPrice, out decimal contentValue))
                        {
                            product.Price = contentValue;
                            break;
                        }
                    }
                    
                    if (_sanitizer.TryParsePrice(priceText, out decimal priceValue))
                    {
                        product.Price = priceValue;
                        break;
                    }
                }
            }
            
            // Старая цена (если есть скидка)
            var oldPriceSelectors = new[]
            {
                ".price-old",
                ".old-price",
                ".previous-price",
                ".discount-old",
                ".goods-old-price",
                ".price-block-old",
            };

            foreach (var selector in oldPriceSelectors)
            {
                var oldPriceElement = document.QuerySelector(selector);
                if (oldPriceElement != null)
                {
                    var oldPriceText = oldPriceElement.TextContent?.Trim();
                    if (_sanitizer.TryParsePrice(oldPriceText, out decimal oldPrice))
                    {
                        product.OldPrice = oldPrice;
                        break;
                    }
                }
            }
            
            // Если цена не найдена, ищем в мета-тегах
            if (product.Price == null)
            {
                var metaPrice = document.QuerySelector("meta[itemprop='price']");
                if (metaPrice != null)
                {
                    var priceText = metaPrice.GetAttribute("content");
                    if (_sanitizer.TryParsePrice(priceText, out decimal price))
                    {
                        product.Price = price;
                    }
                }
            }
        }
            
        /// <summary>
        /// Извлечение бренда
        /// </summary>
        private string ExtractBrand(IDocument document)
        {
            // Приоритетные селекторы для бренда
            var brandSelectors = new[]
            {
                ".brand-value",
                ".product-brand",
                ".goods-brand",
                "[itemprop='brand']",
                ".manufacturer",
                ".brand",
            };

            foreach (var selector in brandSelectors)
            {
                var brandElement = document.QuerySelector(selector);
                if (brandElement != null)
                {
                    var text = brandElement.TextContent?.Trim();
                    if (!string.IsNullOrEmpty(text) && text.Length > 1)
                    {
                        return _sanitizer.CleanBrand(text);
                    }
                }
            }
            
            // Ищем в характеристиках
            var specSelectors = new[]
            {
                ".specifications",
                ".product-attributes",
                ".product-specs",
                ".goods-properties",
                ".characteristics",
            };

            foreach (var specSelector in specSelectors)
            {
                var specTable = document.QuerySelector(specSelector);
                if (specTable != null)
                {
                    var brand = FindBrandInSpecifications(specTable);
                    if (!string.IsNullOrEmpty(brand))
                    {
                        return brand;
                    }
                }
            }
            
            // Fallback: извлекаем первое слово из названия (если это похоже на бренд)
            var productName = ExtractProductName(document);
            var firstWord = productName.Split(' ').FirstOrDefault();
            if (!string.IsNullOrEmpty(firstWord) && 
                firstWord.Length > 2 && 
                firstWord.Length < 30 &&
                !firstWord.Any(char.IsDigit))
            {
                return _sanitizer.CleanBrand(firstWord);
            }
            
            return "Неизвестно";
        }

        /// <summary>
        /// Поиск бренда в спецификациях
        /// </summary>
        private string FindBrandInSpecifications(IElement specTable)
        {
            var rowSelectors = new[] { "tr", ".spec-row", ".property", ".attribute-row" };
            var labelSelectors = new[] { "td", "th", ".spec-name", ".property-name", ".attribute-name" };
            var valueSelectors = new[] { "td", ".spec-value", ".property-value", ".attribute-value" };

            foreach (var rowSelector in rowSelectors)
            {
                var rows = specTable.QuerySelectorAll(rowSelector);
                foreach (var row in rows)
                {
                    var cells = row.QuerySelectorAll(string.Join(", ", labelSelectors));
                    if (cells.Length >= 1)
                    {
                        var label = cells[0].TextContent?.Trim().ToLower() ?? "";
                        
                        // Проверяем, что это поле бренда
                        if (label.Contains("бренд") || label.Contains("производитель") || 
                            label.Contains("марка") || label.Contains("brand") || label.Contains("maker"))
                        {
                            // Ищем значение в соседних ячейках
                            var allCells = row.QuerySelectorAll("td, th");
                            var cellIndex = Array.IndexOf(cells.ToArray(), cells[0]);
                            if (allCells.Length > cellIndex + 1)
                            {
                                var value = allCells[cellIndex + 1].TextContent?.Trim();
                                if (!string.IsNullOrEmpty(value))
                                {
                                    return value;
                                }
                            }
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Извлечение артикула
        /// </summary>
        private string ExtractArticle(IDocument document)
        {
            var articleElement = document.QuerySelector(".article, .sku, [itemprop='sku'], .goods-article");
            if (articleElement != null)
            {
                return articleElement.TextContent?.Trim() ?? "";
            }
            
            // Ищем в тексте "Артикул:"
            var text = document.Body?.TextContent ?? "";
            var articleIndex = text.IndexOf("Артикул:");
            if (articleIndex >= 0)
            {
                var substring = text.Substring(articleIndex, Math.Min(50, text.Length - articleIndex));
                var match = System.Text.RegularExpressions.Regex.Match(substring, @"Артикул:\s*(\w+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Ищем в характеристиках
            var specTable = document.QuerySelector(".specifications, .product-specs");
            if (specTable != null)
            {
                var rows = specTable.QuerySelectorAll("tr, .spec-row");
                foreach (var row in rows)
                {
                    var cells = row.QuerySelectorAll("td, .spec-name, .spec-value");
                    if (cells.Length >= 2)
                    {
                        var label = cells[0].TextContent?.Trim().ToLower();
                        if (label != null && (label.Contains("артикул") || label.Contains("код") || label.Contains("sku")))
                        {
                            return cells[1].TextContent?.Trim() ?? "";
                        }
                    }
                }
            }
            
            return "";
        }

        /// <summary>
        /// Извлечение информации о наличии
        /// </summary>
        private void ExtractAvailability(IDocument document, Product product)
        {
            var availabilityElement = document.QuerySelector(".availability, .stock, .in-stock, .goods-availability");
            if (availabilityElement != null)
            {
                var availabilityText = availabilityElement.TextContent?.Trim().ToLower() ?? "";
                product.AvailabilityDetails = availabilityText;
                
                if (availabilityText.Contains("в наличии") || availabilityText.Contains("есть в наличии") || availabilityText.Contains("доступен"))
                {
                    product.Availability = AvailabilityStatus.InStock;
                }
                else if (availabilityText.Contains("нет в наличии") || availabilityText.Contains("отсутствует") || availabilityText.Contains("распродано"))
                {
                    product.Availability = AvailabilityStatus.OutOfStock;
                }
                else if (availabilityText.Contains("ограниченно") || availabilityText.Contains("мало") || availabilityText.Contains("последний"))
                {
                    product.Availability = AvailabilityStatus.Limited;
                }
                else if (availabilityText.Contains("под заказ") || availabilityText.Contains("предзаказ") || availabilityText.Contains("ожидается"))
                {
                    product.Availability = AvailabilityStatus.PreOrder;
                }
                else
                {
                    product.Availability = AvailabilityStatus.Unknown;
                }
            }
            else
            {
                // Проверяем по наличию кнопки "купить"
                var buyButton = document.QuerySelector(".buy-button, .add-to-cart, .to-cart");
                if (buyButton != null && !buyButton.HasAttribute("disabled"))
                {
                    product.Availability = AvailabilityStatus.InStock;
                    product.AvailabilityDetails = "В наличии (определено по активной кнопке покупки)";
                }
                else
                {
                    product.Availability = AvailabilityStatus.Unknown;
                    product.AvailabilityDetails = "Неизвестно";
                }
            }
        }

        /// <summary>
        /// Извлечение характеристик товара
        /// </summary>
        private Dictionary<string, string> ExtractSpecifications(IDocument document)
        {
            var specifications = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Приоритетные селекторы для таблицы характеристик
            var tableSelectors = new[]
            {
                ".specifications",
                ".product-attributes",
                ".characteristics-table",
                ".product-specs",
                ".characteristics",
                ".goods-properties",
                "table.specifications",
                "table.product-attributes",
            };

            IElement specTable = null;
            foreach (var selector in tableSelectors)
            {
                specTable = document.QuerySelector(selector);
                if (specTable != null) break;
            }
            
            if (specTable == null) return specifications;
            
            var rowSelectors = new[] { "tr", ".spec-row", ".property-row", ".attribute-row", ".spec-item" };
            var cellSelectors = new[] { "td, th", ".spec-name", ".spec-value", ".property-name", ".property-value" };

            foreach (var rowSelector in rowSelectors)
            {
                var rows = specTable.QuerySelectorAll(rowSelector);
                
                foreach (var row in rows)
                {
                    // Пробуем найти ячейки разными способами
                    var allCells = row.QuerySelectorAll("td, th");
                    var namedCells = row.QuerySelectorAll(string.Join(", ", cellSelectors));
                    
                    var cells = allCells.Length >= 2 ? allCells : namedCells;
                    
                    if (cells.Length >= 2)
                    {
                        var key = CleanText(cells[0].TextContent);
                        var value = CleanText(cells[1].TextContent);
                        
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            // Нормализация ключей через DataSanitizer
                            var normalizedKey = _sanitizer.NormalizeSpecificationKey(key);
                            specifications[normalizedKey] = value;
                        }
                    }
                }
            }
            
            return specifications;
        }

        /// <summary>
        /// Извлечение рейтинга и количества отзывов
        /// </summary>
        private void ExtractRating(IDocument document, Product product)
        {
            var ratingElement = document.QuerySelector(".rating, .product-rating, [itemprop='ratingValue'], .goods-rating");
            if (ratingElement != null)
            {
                var ratingText = ratingElement.TextContent?.Trim();
                if (double.TryParse(ratingText?.Replace(",", "."), out double rating))
                {
                    product.Rating = rating;
                }
            }
            
            var reviewElement = document.QuerySelector(".review-count, [itemprop='reviewCount'], .goods-reviews");
            if (reviewElement != null)
            {
                var reviewText = reviewElement.TextContent?.Trim();
                if (int.TryParse(reviewText, out int reviewCount))
                {
                    product.ReviewCount = reviewCount;
                }
            }
        }

        /// <summary>
        /// Извлечение категории товара
        /// </summary>
        private string ExtractCategory(IDocument document)
        {
            var breadcrumbs = document.QuerySelector(".breadcrumbs, .breadcrumb, .goods-breadcrumbs");
            if (breadcrumbs != null)
            {
                var items = breadcrumbs.QuerySelectorAll("a");
                if (items.Length >= 2)
                {
                    // Предпоследний элемент - обычно категория
                    return items[items.Length - 2].TextContent?.Trim() ?? "";
                }
            }
            
            // Ищем в мета-тегах
            var metaCategory = document.QuerySelector("meta[property='product:category']");
            if (metaCategory != null)
            {
                return metaCategory.GetAttribute("content") ?? "";
            }
            
            return "";
        }
    }
}