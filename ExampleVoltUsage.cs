using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser
{
    class ExampleVoltUsage
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Пример использования парсеров для 220-volt.ru ===");
            Console.WriteLine();
            
            // Пример 1: Базовое использование VoltParserService
            Console.WriteLine("1. Базовое использование VoltParserService:");
            Console.WriteLine("```csharp");
            Console.WriteLine("using var parserService = new VoltParserService();");
            Console.WriteLine();
            Console.WriteLine("// Получение категорий");
            Console.WriteLine("var categories = await parserService.GetCategoriesAsync();");
            Console.WriteLine();
            Console.WriteLine("// Парсинг товаров из первой категории");
            Console.WriteLine("if (categories.Count > 0)");
            Console.WriteLine("{");
            Console.WriteLine("    var products = await parserService.GetProductsFromCategoryAsync(");
            Console.WriteLine("        categories[0].Url, maxPages: 2);");
            Console.WriteLine("    ");
            Console.WriteLine("    // Экспорт в CSV");
            Console.WriteLine("    await parserService.ExportToCsvAsync(products, \"220volt_products.csv\");");
            Console.WriteLine("}");
            Console.WriteLine("```");
            Console.WriteLine();
            
            // Пример 2: Использование отдельных парсеров
            Console.WriteLine("2. Использование отдельных парсеров:");
            Console.WriteLine("```csharp");
            Console.WriteLine("// Создание зависимостей");
            Console.WriteLine("var httpClient = new HttpClientService();");
            Console.WriteLine("var categoryParser = new VoltCategoryParser(httpClient);");
            Console.WriteLine("var productParser = new VoltProductParser(httpClient);");
            Console.WriteLine();
            Console.WriteLine("// Получение категорий");
            Console.WriteLine("var categories = await categoryParser.GetCategoriesAsync();");
            Console.WriteLine();
            Console.WriteLine("// Получение URL товаров из категории");
            Console.WriteLine("var productUrls = await categoryParser.GetProductUrlsFromCategoryAsync(");
            Console.WriteLine("    categories[0].Url, maxPages: 3);");
            Console.WriteLine();
            Console.WriteLine("// Парсинг товаров");
            Console.WriteLine("var products = await productParser.ParseProductsAsync(");
            Console.WriteLine("    productUrls, maxConcurrent: 4);");
            Console.WriteLine();
            Console.WriteLine("// Очистка ресурсов");
            Console.WriteLine("httpClient.Dispose();");
            Console.WriteLine("```");
            Console.WriteLine();
            
            // Пример 3: Кастомизация
            Console.WriteLine("3. Кастомизация парсеров:");
            Console.WriteLine("```csharp");
            Console.WriteLine("// Создание логгера с цветным выводом");
            Console.WriteLine("public class ColoredLogger : ILogger");
            Console.WriteLine("{");
            Console.WriteLine("    public void Log(string message)");
            Console.WriteLine("    {");
            Console.WriteLine("        Console.ForegroundColor = ConsoleColor.Cyan;");
            Console.WriteLine("        Console.WriteLine($\"[{DateTime.Now:HH:mm:ss}] {message}\");");
            Console.WriteLine("        Console.ResetColor();");
            Console.WriteLine("    }");
            Console.WriteLine("}");
            Console.WriteLine();
            Console.WriteLine("// Использование кастомного логгера");
            Console.WriteLine("var logger = new ColoredLogger();");
            Console.WriteLine("var httpClient = new HttpClientService(logger, userAgent: \"MyCustomBot/1.0\");");
            Console.WriteLine("var parser = new VoltParserService(logger);");
            Console.WriteLine("```");
            Console.WriteLine();
            
            // Пример 4: Обработка ошибок
            Console.WriteLine("4. Обработка ошибок с RetryPolicy:");
            Console.WriteLine("```csharp");
            Console.WriteLine("var retryPolicy = new RetryPolicy(");
            Console.WriteLine("    maxRetries: 5,");
            Console.WriteLine("    initialDelayMs: 2000,");
            Console.WriteLine("    backoffMultiplier: 2.0,");
            Console.WriteLine("    maxDelayMs: 30000);");
            Console.WriteLine();
            Console.WriteLine("try");
            Console.WriteLine("{");
            Console.WriteLine("    var result = await retryPolicy.ExecuteWithRetryAsync(async () =>");
            Console.WriteLine("    {");
            Console.WriteLine("        // Код, который может вызвать временные ошибки сети");
            Console.WriteLine("        return await SomeNetworkOperationAsync();");
            Console.WriteLine("    }, ");
            Console.WriteLine("    shouldRetry: ex => ex.IsTransientNetworkError());");
            Console.WriteLine("}");
            Console.WriteLine("catch (Exception ex)");
            Console.WriteLine("{");
            Console.WriteLine("    Console.WriteLine($\"Все попытки завершились ошибкой: {ex.Message}\");");
            Console.WriteLine("}");
            Console.WriteLine("```");
            Console.WriteLine();
            
            // Пример 5: Работа с динамической подгрузкой товаров
            Console.WriteLine("5. Работа с динамической подгрузкой товаров:");
            Console.WriteLine("```csharp");
            Console.WriteLine("// Для сайтов с динамической подгрузкой可能需要 дополнительные меры:");
            Console.WriteLine("// 1. Увеличить задержки между запросами");
            Console.WriteLine("// 2. Использовать больше попыток");
            Console.WriteLine("// 3. Эмулировать поведение браузера");
            Console.WriteLine();
            Console.WriteLine("var httpClient = new HttpClientService(");
            Console.WriteLine("    userAgent: \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) \" +");
            Console.WriteLine("               \"AppleWebKit/537.36 (KHTML, like Gecko) \" +");
            Console.WriteLine("               \"Chrome/120.0.0.0 Safari/537.36\");");
            Console.WriteLine();
            Console.WriteLine("// Добавляем дополнительные заголовки");
            Console.WriteLine("// httpClient.AddHeader(\"Accept\", \"application/json\");");
            Console.WriteLine();
            Console.WriteLine("// Используем увеличенные задержки");
            Console.WriteLine("var retryPolicy = new RetryPolicy(");
            Console.WriteLine("    maxRetries: 5,");
            Console.WriteLine("    initialDelayMs: 3000); // 3 секунды");
            Console.WriteLine("```");
            Console.WriteLine();
            
            // Рекомендации по селекторам
            Console.WriteLine("=== Рекомендации по настройке селекторов ===");
            Console.WriteLine();
            Console.WriteLine("Если парсер не находит данные,可能需要 обновить CSS-селекторы:");
            Console.WriteLine("1. Откройте сайт 220-volt.ru в браузере");
            Console.WriteLine("2. Используйте инструменты разработчика (F12)");
            Console.WriteLine("3. Изучите структуру HTML элементов");
            Console.WriteLine("4. Обновите селекторы в VoltCategoryParser.cs и VoltProductParser.cs");
            Console.WriteLine();
            Console.WriteLine("Типичные элементы для 220-volt.ru:");
            Console.WriteLine("- Категории: .catalog-section, .category-item, .left-menu a");
            Console.WriteLine("- Товары: .product-card, .goods-item, .catalog-item");
            Console.WriteLine("- Название: .product-title, h1.product-name, .goods-header h1");
            Console.WriteLine("- Цена: .price, .goods-price, .product-price");
            Console.WriteLine("- Характеристики: .specifications-table, .goods-properties");
            Console.WriteLine("- Наличие: .availability, .goods-availability, .stock");
            Console.WriteLine();
            
            Console.WriteLine("=== Готовые примеры кода ===");
            Console.WriteLine();
            Console.WriteLine("Полный рабочий пример смотрите в файле TestVoltParser.cs");
            Console.WriteLine("Демонстрационный пример - Program.cs");
            Console.WriteLine();
            Console.WriteLine("Для запуска тестового примера:");
            Console.WriteLine("1. Убедитесь, что установлен .NET 8.0+");
            Console.WriteLine("2. Выполните: dotnet build");
            Console.WriteLine("3. Запустите: dotnet run --project VseinstrumentiParser.csproj");
        }
    }
}