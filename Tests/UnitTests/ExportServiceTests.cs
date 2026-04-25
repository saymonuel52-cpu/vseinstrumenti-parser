using Xunit;
using Moq;
using System.IO;
using System.Linq;
using VseinstrumentiParser.Services.Export;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class ExportServiceTests : IDisposable
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly ExportSettings _exportSettings;
        private readonly string _testExportDirectory;

        public ExportServiceTests()
        {
            _loggerMock = new Mock<ILogger>();
            _testExportDirectory = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}");
            
            _exportSettings = new ExportSettings
            {
                OutputDirectory = _testExportDirectory,
                IncludeTimestamp = false,
                DefaultFormat = "CSV"
            };

            // Очищаем тестовую директорию перед каждым тестом
            if (Directory.Exists(_testExportDirectory))
            {
                Directory.Delete(_testExportDirectory, true);
            }
        }

        public void Dispose()
        {
            // Очищаем тестовую директорию после каждого теста
            if (Directory.Exists(_testExportDirectory))
            {
                Directory.Delete(_testExportDirectory, true);
            }
        }

        [Fact]
        public void ExportService_Constructor_CreatesOutputDirectory()
        {
            // Arrange & Act
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);

            // Assert
            Assert.True(Directory.Exists(_testExportDirectory));
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Создана директория для экспорта"))), Times.Once);
        }

        [Fact]
        public void ExportToCsv_WithProducts_CreatesValidCsvFile()
        {
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            var products = new List<Product>
            {
                new Product
                {
                    Name = "Дрель Bosch",
                    Brand = "Bosch",
                    Price = 3499.99m,
                    Currency = "руб.",
                    Availability = "В наличии",
                    Url = "https://example.com/product1",
                    Category = "Дрели",
                    Description = "Мощная дрель",
                    Rating = 4.5f,
                    ReviewCount = 42
                },
                new Product
                {
                    Name = "Шуруповерт Makita",
                    Brand = "Makita",
                    Price = 5299.50m,
                    Currency = "руб.",
                    Availability = "Под заказ",
                    Url = "https://example.com/product2",
                    Category = "Шуруповерты",
                    Description = "Профессиональный шуруповерт",
                    Rating = 4.8f,
                    ReviewCount = 67
                }
            };

            // Act
            var filePath = exportService.ExportToCsv(products, "test_products");

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.EndsWith(".csv", filePath);

            var fileContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Проверяем заголовок
            Assert.Equal("Название;Бренд;Цена;Валюта;Наличие;URL;Категория;Описание;Рейтинг;Количество отзывов", lines[0].Trim());

            // Проверяем количество строк (заголовок + 2 продукта)
            Assert.Equal(3, lines.Length);

            // Проверяем содержимое
            Assert.Contains("Дрель Bosch", lines[1]);
            Assert.Contains("3499.99", lines[1]);
            Assert.Contains("Шуруповерт Makita", lines[2]);
            Assert.Contains("5299.50", lines[2]);

            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Экспорт в CSV завершен"))), Times.Once);
        }

        [Fact]
        public void ExportToJson_WithProducts_CreatesValidJsonFile()
        {
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            var products = new List<Product>
            {
                new Product { Name = "Тестовый продукт", Price = 1000m }
            };

            // Act
            var filePath = exportService.ExportToJson(products, "test_products");

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.EndsWith(".json", filePath);

            var fileContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            Assert.Contains("Тестовый продукт", fileContent);
            Assert.Contains("1000", fileContent);

            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Экспорт в JSON завершен"))), Times.Once);
        }

        [Fact]
        public void ExportCategoriesToCsv_WithCategories_CreatesValidCsvFile()
        {
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            var categories = new List<Category>
            {
                new Category { Name = "Дрели", Url = "https://example.com/drills", ProductCount = 150 },
                new Category { Name = "Шуруповерты", Url = "https://example.com/screwdrivers", ProductCount = 89 }
            };

            // Act
            var filePath = exportService.ExportCategoriesToCsv(categories, "test_categories");

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.EndsWith(".csv", filePath);

            var fileContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Название;URL;Количество товаров", lines[0].Trim());
            Assert.Equal(3, lines.Length); // Заголовок + 2 категории
            Assert.Contains("Дрели", lines[1]);
            Assert.Contains("150", lines[1]);
            Assert.Contains("Шуруповерты", lines[2]);
            Assert.Contains("89", lines[2]);
        }

        [Fact]
        public void ExportFullCatalog_WithCatalog_CreatesMultipleFiles()
        {
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            
            var catalog = new Dictionary<Category, List<Product>>
            {
                {
                    new Category { Name = "Дрели", Url = "https://example.com/drills", ProductCount = 2 },
                    new List<Product>
                    {
                        new Product { Name = "Дрель 1", Price = 1000m },
                        new Product { Name = "Дрель 2", Price = 2000m }
                    }
                },
                {
                    new Category { Name = "Шуруповерты", Url = "https://example.com/screwdrivers", ProductCount = 1 },
                    new List<Product>
                    {
                        new Product { Name = "Шуруповерт 1", Price = 3000m }
                    }
                }
            };

            // Act
            var result = exportService.ExportFullCatalog(catalog, "test_catalog");

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.TotalCategories);
            Assert.Equal(3, result.TotalProducts);
            
            Assert.True(File.Exists(result.CategoriesFile));
            Assert.True(File.Exists(result.ProductsFile));
            Assert.True(File.Exists(result.SummaryFile));
            
            Assert.EndsWith("_categories.csv", result.CategoriesFile);
            Assert.EndsWith("_products.csv", result.ProductsFile);
            Assert.EndsWith("_summary.txt", result.SummaryFile);

            // Проверяем содержимое summary файла
            var summaryContent = File.ReadAllText(result.SummaryFile, System.Text.Encoding.UTF8);
            Assert.Contains("СВОДКА КАТАЛОГА", summaryContent);
            Assert.Contains("Всего категорий: 2", summaryContent);
            Assert.Contains("Всего товаров: 3", summaryContent);
        }

        [Fact]
        public void GetExportedFiles_ReturnsFilesInOrder()
        {
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            
            // Создаем несколько тестовых файлов
            File.WriteAllText(Path.Combine(_testExportDirectory, "file1.csv"), "test1");
            Thread.Sleep(10);
            File.WriteAllText(Path.Combine(_testExportDirectory, "file2.csv"), "test2");
            Thread.Sleep(10);
            File.WriteAllText(Path.Combine(_testExportDirectory, "file3.csv"), "test3");

            // Act
            var files = exportService.GetExportedFiles();

            // Assert
            Assert.Equal(3, files.Count);
            // Файлы должны быть отсортированы по дате создания (новые первыми)
            Assert.EndsWith("file3.csv", files[0]);
            Assert.EndsWith("file2.csv", files[1]);
            Assert.EndsWith("file1.csv", files[2]);
        }

        [Fact]
        public void CleanupExportDirectory_RemovesOldFiles()
        {
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            
            // Создаем 15 тестовых файлов
            for (int i = 1; i <= 15; i++)
            {
                File.WriteAllText(Path.Combine(_testExportDirectory, $"file{i}.csv"), $"test{i}");
                Thread.Sleep(10);
            }

            // Act
            exportService.CleanupExportDirectory(keepLastFiles: 5);

            // Assert
            var remainingFiles = Directory.GetFiles(_testExportDirectory);
            Assert.Equal(5, remainingFiles.Length);
            
            // Должны остаться только самые новые файлы
            var fileNames = remainingFiles.Select(Path.GetFileName).ToList();
            Assert.Contains("file15.csv", fileNames);
            Assert.Contains("file14.csv", fileNames);
            Assert.Contains("file13.csv", fileNames);
            Assert.Contains("file12.csv", fileNames);
            Assert.Contains("file11.csv", fileNames);
            
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Очистка директории экспорта"))), Times.Once);
        }

        [Fact]
        public void EscapeCsvField_HandlesSpecialCharacters()
        {
            // Этот тест проверяет косвенно через экспорт
            // Arrange
            var exportService = new ExportService(_loggerMock.Object, _exportSettings);
            var products = new List<Product>
            {
                new Product
                {
                    Name = "Дрель; с \"кавычками\"",
                    Description = "Многострочное\nописание",
                    Price = 1000m,
                    Currency = "руб.",
                    Availability = "В наличии"
                }
            };

            // Act
            var filePath = exportService.ExportToCsv(products, "test_escape");

            // Assert
            var fileContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            // Поле с точкой с запятой должно быть в кавычках
            Assert.Contains("\"Дрель; с \"\"кавычками\"\"\"", fileContent);
        }
    }
}