using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;
using VseinstrumentiParser.Services.Caching;
using VseinstrumentiParser.Services.Export;
using VseinstrumentiParser.Services.Monitoring;
using VseinstrumentiParser.Utilities;
using VseinstrumentiParser.Models.Configuration;
using VseinstrumentiParser.Services.DependencyInjection;

namespace VseinstrumentiParser
{
    class ProgramWithNewFeatures
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Усовершенствованный парсер каталога электроинструментов ===");
            Console.WriteLine($"Начало работы: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine();

            // Инициализация улучшенного логгера
            var logger = new AdvancedLogger(
                logDirectory: "./logs",
                minLogLevel: AdvancedLogger.LogLevel.Information,
                enableConsoleLogging: true,
                enableFileLogging: true,
                includeTimestamp: true
            );

            try
            {
                // Загрузка конфигурации
                var configService = new ConfigurationService(logger);
                var config = configService.LoadConfiguration();
                
                logger.LogInformation($"Конфигурация загружена. Кэширование: {config.ParserSettings.ParsingLimits.EnableCaching}");

                // Инициализация сервиса мониторинга
                var monitoringService = new MonitoringService(logger);
                var session = monitoringService.StartSession("Полный парсинг каталога", "vseinstrumenti.ru");

                // Инициализация сервисов с использованием DI
                var services = new ServiceCollection();
                services.AddParserServices(logger);
                var serviceProvider = services.BuildServiceProvider();

                // Получение сервисов через DI
                var parserService = serviceProvider.GetService<VseinstrumentiParserServiceWithCache>();
                var exportService = serviceProvider.GetService<ExportService>();
                var cacheService = serviceProvider.GetService<CacheService>();

                // Пример 1: Получение категорий с кэшированием
                Console.WriteLine("1. Получение категорий электроинструментов (с кэшированием)...");
                var categories = await parserService.GetCategoriesAsync(useCache: true);
                
                Console.WriteLine($"Найдено {categories.Count} категорий:");
                foreach (var category in categories.Take(5))
                {
                    Console.WriteLine($"  - {category.Name} ({category.Url}) - {category.ProductCount} товаров");
                }
                if (categories.Count > 5)
                {
                    Console.WriteLine($"  ... и еще {categories.Count - 5} категорий");
                }
                Console.WriteLine();

                // Экспорт категорий в CSV
                var categoriesFile = exportService.ExportCategoriesToCsv(categories, "categories_export");
                Console.WriteLine($"Категории экспортированы в: {categoriesFile}");
                Console.WriteLine();

                // Пример 2: Парсинг товаров из первых двух категорий
                if (categories.Count >= 2)
                {
                    var selectedCategories = categories.Take(2).ToList();
                    var allProducts = new List<Product>();
                    
                    foreach (var category in selectedCategories)
                    {
                        Console.WriteLine($"2. Парсинг товаров из категории: {category.Name}");
                        
                        var products = await parserService.GetProductsFromCategoryAsync(
                            category.Url, 
                            maxPages: 1,
                            useCache: true
                        );
                        
                        Console.WriteLine($"   Найдено {products.Count} товаров:");
                        foreach (var product in products.Take(2))
                        {
                            Console.WriteLine($"     - {product.Name}");
                            Console.WriteLine($"       Цена: {product.PriceFormatted}, Бренд: {product.Brand}, Наличие: {product.Availability}");
                        }
                        if (products.Count > 2)
                        {
                            Console.WriteLine($"     ... и еще {products.Count - 2} товаров");
                        }
                        
                        allProducts.AddRange(products);
                        
                        // Задержка между категориями
                        await Task.Delay(1000);
                    }
                    Console.WriteLine();

                    // Экспорт всех товаров в CSV и JSON
                    var csvFile = exportService.ExportToCsv(allProducts, "products_export");
                    var jsonFile = exportService.ExportToJson(allProducts, "products_export");
                    
                    Console.WriteLine($"Товары экспортированы:");
                    Console.WriteLine($"  - CSV: {csvFile}");
                    Console.WriteLine($"  - JSON: {jsonFile}");
                    Console.WriteLine();
                }

                // Пример 3: Полный парсинг каталога (ограниченный)
                Console.WriteLine("3. Запуск ограниченного полного парсинга каталога...");
                Console.WriteLine("   (парсим по 1 странице из каждой категории)");
                
                var fullCatalog = await parserService.GetAllProductsAsync(
                    maxProductsPerCategory: 5,
                    useCache: true
                );
                
                var totalProducts = fullCatalog.Values.Sum(p => p.Count);
                Console.WriteLine($"   Обработано категорий: {fullCatalog.Count}");
                Console.WriteLine($"   Всего товаров: {totalProducts}");
                Console.WriteLine();

                // Экспорт полного каталога
                var exportResult = exportService.ExportFullCatalog(fullCatalog, "full_catalog");
                if (exportResult.Success)
                {
                    Console.WriteLine($"Полный каталог экспортирован:");
                    Console.WriteLine($"  - Категории: {exportResult.CategoriesFile}");
                    Console.WriteLine($"  - Товары: {exportResult.ProductsFile}");
                    Console.WriteLine($"  - Сводка: {exportResult.SummaryFile}");
                }
                Console.WriteLine();

                // Пример 4: Работа с кэшем
                Console.WriteLine("4. Работа с кэшем:");
                var cacheStats = parserService.GetCacheStatistics();
                Console.WriteLine($"   Всего записей в кэше: {cacheStats.TotalItems}");
                Console.WriteLine($"   Действительных записей: {cacheStats.ValidItems}");
                Console.WriteLine($"   Устаревших записей: {cacheStats.ExpiredItems}");
                Console.WriteLine($"   Использование памяти: {cacheStats.MemoryUsageMB:F2} MB");
                Console.WriteLine();

                // Пример 5: Мониторинг и метрики
                Console.WriteLine("5. Метрики производительности:");
                var metrics = monitoringService.GetCurrentMetrics();
                Console.WriteLine($"   Время работы: {metrics.Uptime.TotalSeconds:F1} сек.");
                Console.WriteLine($"   Обработано товаров: {metrics.TotalProductsParsed}");
                Console.WriteLine($"   Выполнено запросов: {metrics.TotalRequests}");
                Console.WriteLine($"   Ошибок: {metrics.TotalErrors}");
                Console.WriteLine($"   Скорость: {metrics.ProductsPerSecond:F2} товаров/сек.");
                Console.WriteLine();

                // Завершение сессии мониторинга
                monitoringService.EndSession(
                    session.Id, 
                    success: true,
                    categoriesParsed: categories.Count,
                    productsParsed: totalProducts
                );

                // Экспорт метрик в файл
                var metricsFile = monitoringService.ExportMetrics();
                Console.WriteLine($"Метрики экспортированы в: {metricsFile}");
                Console.WriteLine();

                // Очистка старых файлов экспорта
                exportService.CleanupExportDirectory(keepLastFiles: 5);
                Console.WriteLine("Старые файлы экспорта очищены (оставлено 5 последних файлов)");
                Console.WriteLine();

                // Статистика логгера
                var logStats = logger.GetStatistics();
                Console.WriteLine($"Статистика логирования:");
                Console.WriteLine($"   Файлов логов: {logStats.TotalLogFiles}");
                Console.WriteLine($"   Общий размер: {logStats.TotalSizeMB:F2} MB");
                Console.WriteLine($"   Текущий файл: {Path.GetFileName(logStats.CurrentLogFile)}");
                Console.WriteLine($"   Размер текущего файла: {logStats.CurrentLogFileSizeMB:F2} MB");
                Console.WriteLine();

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Критическая ошибка в работе парсера");
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.WriteLine($"Подробности в лог-файле");
            }
            finally
            {
                Console.WriteLine($"Работа завершена: {DateTime.Now:HH:mm:ss}");
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
            }
        }
    }
}