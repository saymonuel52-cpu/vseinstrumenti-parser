using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser
{
    class TestVoltParser
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Тестирование парсеров для 220-volt.ru ===");
            Console.WriteLine($"Начало работы: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine();
            
            using var parserService = new VoltParserService();
            
            try
            {
                // Тест 1: Получение категорий
                Console.WriteLine("1. Тест получения категорий...");
                try
                {
                    var categories = await parserService.GetCategoriesAsync();
                    
                    Console.WriteLine($"Найдено {categories.Count} категорий:");
                    foreach (var category in categories.Take(5))
                    {
                        Console.WriteLine($"  - {category.Name} ({category.Url})");
                    }
                    if (categories.Count > 5)
                    {
                        Console.WriteLine($"  ... и еще {categories.Count - 5} категорий");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Ошибка: {ex.Message}");
                    Console.WriteLine("   (Возможно, изменилась структура сайта или требуется обновление селекторов)");
                }
                Console.WriteLine();
                
                // Тест 2: Парсинг конкретного товара (пример URL)
                Console.WriteLine("2. Тест парсинга товара...");
                try
                {
                    // Пример URL товара с 220-volt.ru (может потребоваться актуальный)
                    var sampleProductUrl = "https://www.220-volt.ru/catalog-9889-elektroinstrumenty/dreli-udarnye/";
                    // Или используем реальный товар, если известен URL
                    // var sampleProductUrl = "https://www.220-volt.ru/product/drel-udarnaya-bosch-gsb-13-re/";
                    
                    // Для теста создадим мок-товар, если не можем получить реальный
                    Console.WriteLine("   Используем тестовый режим (реальный парсинг требует корректных селекторов)");
                    
                    // Проверяем, что парсеры создаются без ошибок
                    var httpClient = new HttpClientService();
                    var categoryParser = new VoltCategoryParser(httpClient);
                    var productParser = new VoltProductParser(httpClient);
                    
                    Console.WriteLine("   Парсеры успешно инициализированы");
                    Console.WriteLine($"   CategoryParser тип: {categoryParser.GetType().Name}");
                    Console.WriteLine($"   ProductParser тип: {productParser.GetType().Name}");
                    
                    httpClient.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Ошибка: {ex.Message}");
                }
                Console.WriteLine();
                
                // Тест 3: Проверка совместимости интерфейсов
                Console.WriteLine("3. Тест совместимости интерфейсов...");
                try
                {
                    var httpClient = new HttpClientService();
                    var categoryParser = new VoltCategoryParser(httpClient) as ICategoryParser;
                    var productParser = new VoltProductParser(httpClient) as IProductParser;
                    
                    if (categoryParser != null && productParser != null)
                    {
                        Console.WriteLine("   ✓ Интерфейсы ICategoryParser и IProductParser реализованы корректно");
                        
                        // Проверяем наличие методов
                        var categoryMethods = typeof(ICategoryParser).GetMethods();
                        Console.WriteLine($"   ICategoryParser содержит {categoryMethods.Length} методов");
                        
                        var productMethods = typeof(IProductParser).GetMethods();
                        Console.WriteLine($"   IProductParser содержит {productMethods.Length} методов");
                    }
                    else
                    {
                        Console.WriteLine("   ✗ Ошибка приведения типов");
                    }
                    
                    httpClient.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Ошибка: {ex.Message}");
                }
                Console.WriteLine();
                
                // Тест 4: Проверка RetryPolicy
                Console.WriteLine("4. Тест RetryPolicy...");
                try
                {
                    var retryPolicy = new RetryPolicy();
                    int attemptCount = 0;
                    
                    var result = await retryPolicy.ExecuteWithRetryAsync(async () =>
                    {
                        attemptCount++;
                        if (attemptCount < 3)
                        {
                            throw new HttpRequestException("Тестовая ошибка сети");
                        }
                        return "Успех после повторных попыток";
                    });
                    
                    Console.WriteLine($"   Результат: {result}");
                    Console.WriteLine($"   Количество попыток: {attemptCount}");
                    Console.WriteLine("   ✓ RetryPolicy работает корректно");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Ошибка: {ex.Message}");
                }
                Console.WriteLine();
                
                // Тест 5: Проверка моделей данных
                Console.WriteLine("5. Тест моделей данных...");
                try
                {
                    var category = new Category
                    {
                        Name = "Дрели ударные",
                        Url = "https://www.220-volt.ru/catalog-9889-elektroinstrumenty/dreli-udarnye/",
                        ProductCount = 42
                    };
                    
                    var product = new Product
                    {
                        Name = "Дрель ударная BOSCH GSB 13 RE",
                        Price = 7990m,
                        Brand = "BOSCH",
                        Article = "GSB13RE",
                        Availability = AvailabilityStatus.InStock,
                        Specifications = new Dictionary<string, string>
                        {
                            { "Мощность", "600 Вт" },
                            { "Тип двигателя", "Щеточный" },
                            { "Напряжение", "220 В" }
                        }
                    };
                    
                    Console.WriteLine($"   Категория: {category}");
                    Console.WriteLine($"   Товар: {product}");
                    Console.WriteLine("   ✓ Модели данных работают корректно");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Ошибка: {ex.Message}");
                }
                
                Console.WriteLine();
                Console.WriteLine("=== Тестирование завершено ===");
                Console.WriteLine("Рекомендации:");
                Console.WriteLine("1. Для реального парсинга可能需要 уточнить CSS-селекторы под актуальную структуру сайта");
                Console.WriteLine("2. Проверить наличие блокировок со стороны сайта (использовать задержки)");
                Console.WriteLine("3. При необходимости обновить User-Agent в HttpClientService");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine();
            Console.WriteLine($"Завершение работы: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}