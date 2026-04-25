using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Парсер каталога электроинструментов vseinstrumenti.ru ===");
            Console.WriteLine($"Начало работы: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine();
            
            using var parserService = new VseinstrumentiParserService();
            
            try
            {
                // Пример 1: Получение категорий
                Console.WriteLine("1. Получение категорий электроинструментов...");
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
                Console.WriteLine();
                
                // Пример 2: Парсинг товаров из первой категории
                if (categories.Count > 0)
                {
                    var firstCategory = categories[0];
                    Console.WriteLine($"2. Парсинг товаров из категории: {firstCategory.Name}");
                    
                    var products = await parserService.GetProductsFromCategoryAsync(firstCategory.Url, maxPages: 1);
                    
                    Console.WriteLine($"Найдено {products.Count} товаров:");
                    foreach (var product in products.Take(3))
                    {
                        Console.WriteLine($"  - {product.Name}");
                        Console.WriteLine($"    Цена: {product.Price} руб., Бренд: {product.Brand}, Наличие: {product.Availability}");
                    }
                    if (products.Count > 3)
                    {
                        Console.WriteLine($"  ... и еще {products.Count - 3} товаров");
                    }
                    Console.WriteLine();
                    
                    // Пример 3: Экспорт в CSV
                    if (products.Count > 0)
                    {
                        Console.WriteLine("3. Экспорт товаров в CSV...");
                        await parserService.ExportToCsvAsync(products, "products_export.csv");
                        Console.WriteLine("Экспорт завершен: products_export.csv");
                        Console.WriteLine();
                    }
                }
                
                // Пример 4: Получение деталей конкретного товара
                Console.WriteLine("4. Парсинг конкретного товара...");
                try
                {
                    // Пример URL товара (может потребоваться актуальный URL)
                    var sampleProductUrl = "https://www.vseinstrumenti.ru/product/drel-udarnaya-bosch-gsb-13-re-0-601-9a0-100-723663/";
                    var productDetails = await parserService.GetProductDetailsAsync(sampleProductUrl);
                    
                    Console.WriteLine($"Товар: {productDetails.Name}");
                    Console.WriteLine($"Цена: {productDetails.Price} руб.");
                    Console.WriteLine($"Бренд: {productDetails.Brand}");
                    Console.WriteLine($"Артикул: {productDetails.Article}");
                    Console.WriteLine($"Наличие: {productDetails.AvailabilityDetails}");
                    Console.WriteLine($"Характеристик: {productDetails.Specifications.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при парсинге товара: {ex.Message}");
                    Console.WriteLine("(Это ожидаемо, если URL не существует или изменилась структура сайта)");
                }
                
                Console.WriteLine();
                Console.WriteLine("=== Парсинг завершен успешно ===");
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