using System.Globalization;
using System.Text;
using System.Text.Json;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services.Export
{
    /// <summary>
    /// Сервис для экспорта данных в различные форматы
    /// </summary>
    public class ExportService
    {
        private readonly ILogger _logger;
        private readonly ExportSettings _settings;

        /// <summary>
        /// Конструктор
        /// </summary>
        public ExportService(ILogger logger, ExportSettings settings)
        {
            _logger = logger;
            _settings = settings;
            
            // Создаем директорию для экспорта если не существует
            if (!Directory.Exists(_settings.OutputDirectory))
            {
                Directory.CreateDirectory(_settings.OutputDirectory);
                _logger.Log($"Создана директория для экспорта: {_settings.OutputDirectory}");
            }
        }

        /// <summary>
        /// Экспорт списка товаров в CSV
        /// </summary>
        public string ExportToCsv(List<Product> products, string? filename = null)
        {
            try
            {
                var outputPath = GetOutputPath(filename ?? "products", "csv");
                
                using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
                
                // Заголовок CSV
                writer.WriteLine("Название;Бренд;Цена;Валюта;Наличие;URL;Категория;Описание;Рейтинг;Количество отзывов");
                
                // Данные
                foreach (var product in products)
                {
                    var line = string.Join(";",
                        EscapeCsvField(product.Name),
                        EscapeCsvField(product.Brand),
                        product.Price.ToString(CultureInfo.InvariantCulture),
                        EscapeCsvField(product.Currency),
                        EscapeCsvField(product.Availability),
                        EscapeCsvField(product.Url),
                        EscapeCsvField(product.Category),
                        EscapeCsvField(product.Description),
                        product.Rating.ToString(CultureInfo.InvariantCulture),
                        product.ReviewCount.ToString()
                    );
                    
                    writer.WriteLine(line);
                }
                
                _logger.Log($"Экспорт в CSV завершен: {products.Count} товаров, файл: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при экспорте в CSV: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Экспорт списка товаров в JSON
        /// </summary>
        public string ExportToJson(List<Product> products, string? filename = null)
        {
            try
            {
                var outputPath = GetOutputPath(filename ?? "products", "json");
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var json = JsonSerializer.Serialize(products, options);
                File.WriteAllText(outputPath, json, Encoding.UTF8);
                
                _logger.Log($"Экспорт в JSON завершен: {products.Count} товаров, файл: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при экспорте в JSON: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Экспорт категорий в CSV
        /// </summary>
        public string ExportCategoriesToCsv(List<Category> categories, string? filename = null)
        {
            try
            {
                var outputPath = GetOutputPath(filename ?? "categories", "csv");
                
                using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
                
                // Заголовок CSV
                writer.WriteLine("Название;URL;Количество товаров");
                
                // Данные
                foreach (var category in categories)
                {
                    var line = string.Join(";",
                        EscapeCsvField(category.Name),
                        EscapeCsvField(category.Url),
                        category.ProductCount.ToString()
                    );
                    
                    writer.WriteLine(line);
                }
                
                _logger.Log($"Экспорт категорий в CSV завершен: {categories.Count} категорий, файл: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при экспорте категорий в CSV: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Экспорт полного каталога (категории + товары) в несколько файлов
        /// </summary>
        public ExportResult ExportFullCatalog(Dictionary<Category, List<Product>> catalog, string? baseFilename = null)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = baseFilename ?? $"catalog_{timestamp}";
                
                // Экспорт категорий
                var categories = catalog.Keys.ToList();
                var categoriesPath = ExportCategoriesToCsv(categories, $"{filename}_categories");
                
                // Экспорт всех товаров
                var allProducts = catalog.Values.SelectMany(p => p).ToList();
                var productsPath = ExportToCsv(allProducts, $"{filename}_products");
                
                // Создание summary файла
                var summaryPath = CreateSummaryFile(catalog, filename);
                
                _logger.Log($"Полный экспорт каталога завершен:");
                _logger.Log($"  - Категории: {categoriesPath}");
                _logger.Log($"  - Товары: {productsPath}");
                _logger.Log($"  - Сводка: {summaryPath}");
                
                return new ExportResult
                {
                    Success = true,
                    CategoriesFile = categoriesPath,
                    ProductsFile = productsPath,
                    SummaryFile = summaryPath,
                    TotalCategories = categories.Count,
                    TotalProducts = allProducts.Count
                };
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при полном экспорте каталога: {ex.Message}");
                return new ExportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Создание сводного файла с информацией о каталоге
        /// </summary>
        private string CreateSummaryFile(Dictionary<Category, List<Product>> catalog, string baseFilename)
        {
            var outputPath = GetOutputPath($"{baseFilename}_summary", "txt");
            
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            
            writer.WriteLine("=== СВОДКА КАТАЛОГА ===");
            writer.WriteLine($"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            writer.WriteLine($"Всего категорий: {catalog.Count}");
            
            var totalProducts = catalog.Values.Sum(p => p.Count);
            writer.WriteLine($"Всего товаров: {totalProducts}");
            writer.WriteLine();
            
            writer.WriteLine("=== КАТЕГОРИИ ===");
            foreach (var kvp in catalog)
            {
                var category = kvp.Key;
                var products = kvp.Value;
                
                writer.WriteLine($"Категория: {category.Name}");
                writer.WriteLine($"  URL: {category.Url}");
                writer.WriteLine($"  Товаров: {products.Count}");
                
                if (products.Any())
                {
                    var avgPrice = products.Average(p => p.Price);
                    var minPrice = products.Min(p => p.Price);
                    var maxPrice = products.Max(p => p.Price);
                    
                    writer.WriteLine($"  Средняя цена: {avgPrice:F2} руб.");
                    writer.WriteLine($"  Минимальная цена: {minPrice:F2} руб.");
                    writer.WriteLine($"  Максимальная цена: {maxPrice:F2} руб.");
                }
                
                writer.WriteLine();
            }
            
            return outputPath;
        }

        /// <summary>
        /// Получить путь для выходного файла
        /// </summary>
        private string GetOutputPath(string baseName, string extension)
        {
            var filename = _settings.IncludeTimestamp 
                ? $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}"
                : $"{baseName}.{extension}";
            
            return Path.Combine(_settings.OutputDirectory, filename);
        }

        /// <summary>
        /// Экранирование поля для CSV
        /// </summary>
        private string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
            
            // Если поле содержит точку с запятой, кавычки или перенос строки, заключаем в кавычки
            if (field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            
            return field;
        }

        /// <summary>
        /// Получить список экспортированных файлов
        /// </summary>
        public List<string> GetExportedFiles()
        {
            if (!Directory.Exists(_settings.OutputDirectory))
                return new List<string>();
            
            return Directory.GetFiles(_settings.OutputDirectory)
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
        }

        /// <summary>
        /// Очистить директорию экспорта
        /// </summary>
        public void CleanupExportDirectory(int keepLastFiles = 10)
        {
            try
            {
                var files = GetExportedFiles();
                
                if (files.Count > keepLastFiles)
                {
                    var filesToDelete = files.Skip(keepLastFiles).ToList();
                    
                    foreach (var file in filesToDelete)
                    {
                        File.Delete(file);
                    }
                    
                    _logger.Log($"Очистка директории экспорта: удалено {filesToDelete.Count} старых файлов");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при очистке директории экспорта: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Результат экспорта
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CategoriesFile { get; set; }
        public string? ProductsFile { get; set; }
        public string? SummaryFile { get; set; }
        public int TotalCategories { get; set; }
        public int TotalProducts { get; set; }
        
        public override string ToString()
        {
            if (!Success)
                return $"Экспорт не удался: {ErrorMessage}";
            
            return $"Экспорт успешен: {TotalCategories} категорий, {TotalProducts} товаров";
        }
    }
}