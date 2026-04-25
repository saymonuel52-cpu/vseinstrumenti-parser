using AngleSharp;
using AngleSharp.Dom;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Реализация парсера товаров для сайта vseinstrumenti.ru
    /// </summary>
    public class ProductParser : IProductParser
    {
        private readonly HttpClientService _httpClient;
        private readonly ILogger _logger;
        private readonly IBrowsingContext _browsingContext;

        public ProductParser(HttpClientService httpClient, ILogger? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
                string html = await _httpClient.GetHtmlAsync(productUrl);
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
                    await Task.Delay(500);
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
            var nameElement = document.QuerySelector(".product-title, h1.product-name, .item-title");
            if (nameElement != null)
            {
                return nameElement.TextContent?.Trim() ?? "Неизвестно";
            }
            
            // Альтернативные селекторы
            nameElement = document.QuerySelector("h1");
            if (nameElement != null && !string.IsNullOrEmpty(nameElement.TextContent?.Trim()))
            {
                return nameElement.TextContent.Trim();
            }
            
            return "Неизвестно";
        }

        /// <summary>
        /// Извлечение цены
        /// </summary>
        private void ExtractPrice(IDocument document, Product product)
        {
            // Текущая цена
            var priceElement = document.QuerySelector(".price, .product-price, .current-price");
            if (priceElement != null)
            {
                var priceText = priceElement.TextContent?.Trim();
                if (TryParsePrice(priceText, out decimal price))
                {
                    product.Price = price;
                }
            }
            
            // Старая цена (если есть скидка)
            var oldPriceElement = document.QuerySelector(".old-price, .previous-price, .price-old");
            if (oldPriceElement != null)
            {
                var oldPriceText = oldPriceElement.TextContent?.Trim();
                if (TryParsePrice(oldPriceText, out decimal oldPrice))
                {
                    product.OldPrice = oldPrice;
                }
            }
        }

        /// <summary>
        /// Извлечение бренда
        /// </summary>
        private string ExtractBrand(IDocument document)
        {
            // Ищем бренд в различных местах
            var brandElement = document.QuerySelector(".brand, .manufacturer, [itemprop='brand']");
            if (brandElement != null)
            {
                return brandElement.TextContent?.Trim() ?? "Неизвестно";
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
                        if (label != null && (label.Contains("бренд") || label.Contains("производитель") || label.Contains("марка")))
                        {
                            return cells[1].TextContent?.Trim() ?? "Неизвестно";
                        }
                    }
                }
            }
            
            return "Неизвестно";
        }

        /// <summary>
        /// Извлечение артикула
        /// </summary>
        private string ExtractArticle(IDocument document)
        {
            var articleElement = document.QuerySelector(".article, .sku, [itemprop='sku']");
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
            
            return "";
        }

        /// <summary>
        /// Извлечение информации о наличии
        /// </summary>
        private void ExtractAvailability(IDocument document, Product product)
        {
            var availabilityElement = document.QuerySelector(".availability, .stock, .in-stock");
            if (availabilityElement != null)
            {
                var availabilityText = availabilityElement.TextContent?.Trim().ToLower() ?? "";
                product.AvailabilityDetails = availabilityText;
                
                if (availabilityText.Contains("в наличии") || availabilityText.Contains("есть в наличии"))
                {
                    product.Availability = AvailabilityStatus.InStock;
                }
                else if (availabilityText.Contains("нет в наличии") || availabilityText.Contains("отсутствует"))
                {
                    product.Availability = AvailabilityStatus.OutOfStock;
                }
                else if (availabilityText.Contains("ограниченно") || availabilityText.Contains("мало"))
                {
                    product.Availability = AvailabilityStatus.Limited;
                }
                else if (availabilityText.Contains("под заказ") || availabilityText.Contains("предзаказ"))
                {
                    product.Availability = AvailabilityStatus.PreOrder;
                }
                else
                {
                    product.Availability = AvailabilityStatus.Unknown;
                }
            }
        }

        /// <summary>
        /// Извлечение характеристик товара
        /// </summary>
        private Dictionary<string, string> ExtractSpecifications(IDocument document)
        {
            var specifications = new Dictionary<string, string>();
            
            var specTable = document.QuerySelector(".specifications-table, .product-specs, .characteristics");
            if (specTable == null) return specifications;
            
            var rows = specTable.QuerySelectorAll("tr, .spec-row");
            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td, .spec-name, .spec-value");
                if (cells.Length >= 2)
                {
                    var key = cells[0].TextContent?.Trim() ?? "";
                    var value = cells[1].TextContent?.Trim() ?? "";
                    
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    {
                        specifications[key] = value;
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
            var ratingElement = document.QuerySelector(".rating, .product-rating, [itemprop='ratingValue']");
            if (ratingElement != null)
            {
                var ratingText = ratingElement.TextContent?.Trim();
                if (double.TryParse(ratingText?.Replace(",", "."), out double rating))
                {
                    product.Rating = rating;
                }
            }
            
            var reviewElement = document.QuerySelector(".review-count, [itemprop='reviewCount']");
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
            var breadcrumbs = document.QuerySelector(".breadcrumbs, .breadcrumb");
            if (breadcrumbs != null)
            {
                var items = breadcrumbs.QuerySelectorAll("a");
                if (items.Length >= 2)
                {
                    // Предпоследний элемент - обычно категория
                    return items[items.Length - 2].TextContent?.Trim() ?? "";
                }
            }
            
            return "";
        }

        /// <summary>
        /// Парсинг цены из текста
        /// </summary>
        private bool TryParsePrice(string? priceText, out decimal price)
        {
            price = 0;
            if (string.IsNullOrEmpty(priceText)) return false;
            
            // Удаляем всё, кроме цифр и запятой/точки
            var cleanText = System.Text.RegularExpressions.Regex.Replace(priceText, @"[^\d,.]", "");
            cleanText = cleanText.Replace(",", ".");
            
            // Удаляем лишние точки (если это разделитель тысяч)
            if (cleanText.Count(c => c == '.') > 1)
            {
                var lastDotIndex = cleanText.LastIndexOf('.');
                cleanText = cleanText.Remove(lastDotIndex, 1).Insert(lastDotIndex, "");
            }
            
            return decimal.TryParse(cleanText, out price);
        }
    }
}