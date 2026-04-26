using AngleSharp;
using AngleSharp.Dom;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace VoltSelectorAnalyzer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Анализатор селекторов 220-volt.ru ===\n");

        var categoryUrl = "https://www.220-volt.ru/catalog-9889-elektroinstrumenty/";
        var productUrl = "https://www.220-volt.ru/catalog-10125-dreti/";

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        // Устанавливаем заголовки браузера
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

        try
        {
            // Анализируем категорию
            Console.WriteLine($"[1/2] Анализ страницы категории: {categoryUrl}");
            var categoryHtml = await httpClient.GetStringAsync(categoryUrl);
            await AnalyzeCategoryPage(categoryHtml);

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // Анализируем товар
            Console.WriteLine($"[2/2] Анализ страницы товара: {productUrl}");
            var productHtml = await httpClient.GetStringAsync(productUrl);
            await AnalyzeProductPage(productHtml);

            Console.WriteLine("\n=== Анализ завершен! ===");
            Console.WriteLine("Используйте найденные селекторы для обновления VoltCategoryParser.cs и VoltProductParser.cs");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
            Console.WriteLine($"Детали: {ex.GetType().Name}");
        }
        finally
        {
            httpClient.Dispose();
        }
    }

    static async Task AnalyzeCategoryPage(string html)
    {
        var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
        var document = await context.OpenAsync(req => req.Content(html));

        Console.WriteLine("\n📊 СТРУКТУРА КАТЕГОРИИ");
        Console.WriteLine(new string('─', 50));

        // Ищем все ссылки в каталоге
        var allLinks = document.QuerySelectorAll("a[href]");
        Console.WriteLine($"\nВсего ссылок на странице: {allLinks.Length}");

        // Ищем ссылки на подкатегории (с /catalog-)
        var catalogLinks = allLinks.Where(a => a.GetAttribute("href")?.Contains("/catalog-") == true).ToList();
        Console.WriteLine($"Ссылок на каталог (/catalog-): {catalogLinks.Count}");

        if (catalogLinks.Count > 0)
        {
            Console.WriteLine("\nПримеры каталогных ссылок:");
            foreach (var link in catalogLinks.Take(5))
            {
                var href = link.GetAttribute("href");
                var text = link.TextContent?.Trim() ?? "";
                text = Regex.Replace(text, @"\s+", " ");
                Console.WriteLine($"  • {text} -> {href}");
                
                // Выводим родительские классы
                var parent = link.Parent;
                var classes = new List<string>();
                while (parent != null && classes.Count < 3)
                {
                    if (!string.IsNullOrEmpty(parent.ClassList?.Value))
                    {
                        classes.Add(parent.ClassList.Value);
                    }
                    parent = parent.Parent;
                }
                if (classes.Count > 0)
                {
                    Console.WriteLine($"    Селектор: {string.Join(" > ", classes.Reverse())} a");
                }
            }
        }

        // Ищем карточки товаров
        var productCards = document.QuerySelectorAll(".product-card, .goods-item, .catalog-item, .product-item, .item, .goods-card");
        Console.WriteLine($"\nКарточек товаров (по классам): {productCards.Length}");

        foreach (var card in productCards.Take(1))
        {
            Console.WriteLine("\nСтруктура карточки товара:");
            Console.WriteLine($"  Классы: {string.Join(", ", card.ClassList)}");
            
            // Ищем заголовок внутри карточки
            var title = card.QuerySelector("a, h3, h4, .title, .product-name, .goods-title");
            if (title != null)
            {
                Console.WriteLine($"  Заголовок: {title.NodeName} с классом '{title.ClassList?.Value}'");
                Console.WriteLine($"  Текст: {title.TextContent?.Trim().Substring(0, Math.Min(50, title.TextContent?.Length ?? 0))}...");
            }

            // Ищем цену
            var price = card.QuerySelector(".price, .cost, .goods-price, .product-price, [itemprop='price']");
            if (price != null)
            {
                Console.WriteLine($"  Цена: {price.ClassList?.Value}");
                Console.WriteLine($"  Значение: {price.TextContent?.Trim()}");
            }
        }

        // Ищем структуру навигации
        var navMenus = document.QuerySelectorAll("nav, .menu, .navigation, .catalog-menu, .left-menu, .sidebar");
        Console.WriteLine($"\nНавигационных элементов: {navMenus.Length}");

        foreach (var nav in navMenus.Take(2))
        {
            Console.WriteLine($"  • Навигация с классами: {nav.ClassList?.Value}");
        }

        // Сохраняем HTML для ручного анализа
        SaveHtml("volt-category-analysis.html", html);
    }

    static async Task AnalyzeProductPage(string html)
    {
        var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
        var document = await context.OpenAsync(req => req.Content(html));

        Console.WriteLine("\n📊 СТРУКТУРА ТОВАРА");
        Console.WriteLine(new string('─', 50));

        // 1. ЗАГОЛОВОК
        Console.WriteLine("\n1️⃣ ЗАГОЛОВОК ТОВАРА");
        var h1Elements = document.QuerySelectorAll("h1");
        Console.WriteLine($"Всего h1 элементов: {h1Elements.Length}");
        
        foreach (var h1 in h1Elements)
        {
            var text = h1.TextContent?.Trim() ?? "";
            text = Regex.Replace(text, @"\s+", " ");
            Console.WriteLine($"  • Класс: '{h1.ClassList?.Value}'");
            Console.WriteLine($"    Текст: {text}");
            
            // Показываем родительскую структуру
            var parentPath = GetParentPath(h1);
            Console.WriteLine($"    Путь: {parentPath}");
        }

        // 2. ЦЕНА
        Console.WriteLine("\n2️⃣ ЦЕНА");
        var priceElements = document.QuerySelectorAll(".price, .cost, [itemprop='price'], .product-price, .goods-price, .price-block");
        Console.WriteLine($"Элементов цены найдено: {priceElements.Length}");
        
        foreach (var price in priceElements)
        {
            var text = price.TextContent?.Trim() ?? "";
            Console.WriteLine($"  • Класс: '{price.ClassList?.Value}'");
            Console.WriteLine($"    Значение: {text}");
            if (price.HasAttribute("itemprop"))
            {
                Console.WriteLine($"    itemprop: {price.GetAttribute("itemprop")}");
            }
        }

        // Ищем старую цену
        var oldPriceElements = document.QuerySelectorAll(".old-price, .previous-price, .price-old, .discount-price");
        if (oldPriceElements.Length > 0)
        {
            Console.WriteLine($"\nЭлементов старой цены: {oldPriceElements.Length}");
            foreach (var op in oldPriceElements)
            {
                Console.WriteLine($"  • Класс: '{op.ClassList?.Value}' -> {op.TextContent?.Trim()}");
            }
        }

        // 3. БРЕНД
        Console.WriteLine("\n3️⃣ БРЕНД");
        var brandElements = document.QuerySelectorAll(".brand, .manufacturer, [itemprop='brand'], .product-brand, .goods-brand");
        Console.WriteLine($"Элементов бренда найдено: {brandElements.Length}");
        
        foreach (var brand in brandElements)
        {
            var text = brand.TextContent?.Trim() ?? "";
            Console.WriteLine($"  • Класс: '{brand.ClassList?.Value}'");
            Console.WriteLine($"    Значение: {text}");
        }

        // 4. ХАРАКТЕРИСТИКИ
        Console.WriteLine("\n4️⃣ ХАРАКТЕРИСТИКИ");
        var specTables = document.QuerySelectorAll(".specifications, .product-attributes, .characteristics, .product-specs, .goods-properties, table");
        Console.WriteLine($"Таблицей характеристик найдено: {specTables.Length}");
        
        foreach (var table in specTables.Take(1))
        {
            Console.WriteLine($"  • Классы: {table.ClassList?.Value}");
            var rows = table.QuerySelectorAll("tr, .spec-row, .property-row, .attribute-row");
            Console.WriteLine($"    Строк: {rows.Length}");
            
            if (rows.Length > 0)
            {
                var firstRow = rows[0];
                var cells = firstRow.QuerySelectorAll("td, th, .spec-name, .spec-value, .property-name, .property-value");
                Console.WriteLine($"    Ячеек в первой строке: {cells.Length}");
                foreach (var cell in cells.Take(2))
                {
                    Console.WriteLine($"      - {cell.TextContent?.Trim().Substring(0, Math.Min(30, cell.TextContent?.Length ?? 0))}");
                }
            }
        }

        // 5. НАЛИЧИЕ
        Console.WriteLine("\n5️⃣ НАЛИЧИЕ");
        var availabilityElements = document.QuerySelectorAll(".availability, .stock, .in-stock, .goods-availability, .product-status");
        Console.WriteLine($"Элементов наличия найдено: {availabilityElements.Length}");
        
        foreach (var avail in availabilityElements)
        {
            var text = avail.TextContent?.Trim() ?? "";
            Console.WriteLine($"  • Класс: '{avail.ClassList?.Value}'");
            Console.WriteLine($"    Значение: {text}");
        }

        // 6. АРТИКУЛ
        Console.WriteLine("\n6️⃣ АРТИКУЛ");
        var articleElements = document.QuerySelectorAll(".article, .sku, [itemprop='sku'], .product-sku");
        foreach (var art in articleElements)
        {
            Console.WriteLine($"  • Класс: '{art.ClassList?.Value}' -> {art.TextContent?.Trim()}");
        }

        // 7. ИЗОБРАЖЕНИЯ
        Console.WriteLine("\n7️⃣ ИЗОБРАЖЕНИЯ");
        var images = document.QuerySelectorAll("img[src]");
        Console.WriteLine($"Всего изображений: {images.Length}");
        
        var productImages = document.QuerySelectorAll(".product-image, .product-gallery, .image-gallery, .goods-image, [itemprop='image']");
        Console.WriteLine($"Изображений товара: {productImages.Length}");
        
        foreach (var img in productImages.Take(1))
        {
            var src = img.GetAttribute("src") ?? img.GetAttribute("data-src") ?? "";
            Console.WriteLine($"  • Класс: '{img.ClassList?.Value}'");
            Console.WriteLine($"    Src: {src.Substring(0, Math.Min(60, src.Length))}...");
        }

        // 8. MICRODATA (Schema.org)
        Console.WriteLine("\n8️⃣ MICRODATA / SCHEMA.ORG");
        var microdataItems = document.QuerySelectorAll("[itemprop]");
        var microdataProps = microdataItems
            .Select(i => i.GetAttribute("itemprop"))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();
        
        Console.WriteLine($"Уникальных itemprop атрибутов: {microdataProps.Count}");
        Console.WriteLine($"  {string.Join(", ", microdataProps.Take(15))}");

        // 9. ХЛЕБНЫЕ КРОШКИ
        Console.WriteLine("\n9️⃣ ХЛЕБНЫЕ КРОШКИ");
        var breadcrumbs = document.QuerySelectorAll(".breadcrumbs, .breadcrumb, .nav-path, [itemprop='breadcrumb']");
        Console.WriteLine($"Элементов навигации: {breadcrumbs.Length}");
        
        foreach (var bc in breadcrumbs.Take(1))
        {
            var links = bc.QuerySelectorAll("a");
            Console.WriteLine($"  • Ссылок в навигации: {links.Length}");
            foreach (var link in links.Take(3))
            {
                Console.WriteLine($"    - {link.TextContent?.Trim()} -> {link.GetAttribute("href")}");
            }
        }

        // Сохраняем HTML
        SaveHtml("volt-product-analysis.html", html);
    }

    static string GetParentPath(IElement element)
    {
        var parts = new List<string>();
        var current = element;
        
        while (current != null && parts.Count < 5)
        {
            var selector = current.NodeName.ToLower();
            if (!string.IsNullOrEmpty(current.ClassList?.Value))
            {
                selector += "." + string.Join(".", current.ClassList);
            }
            if (!string.IsNullOrEmpty(current.Id))
            {
                selector += "#" + current.Id;
            }
            parts.Add(selector);
            current = current.ParentElement;
        }
        
        return string.Join(" > ", parts.Reverse());
    }

    static void SaveHtml(string filename, string content)
    {
        var path = Path.Combine("test-data", filename);
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine($"\n💾 HTML сохранен: {path}");
    }
}
