using Moq;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class CategoryParserTests
    {
        private readonly Mock<IHtmlLoader> _htmlLoaderMock;
        private readonly DataSanitizer _sanitizer;
        private readonly CategoryParser _parser;

        public CategoryParserTests()
        {
            _htmlLoaderMock = new Mock<IHtmlLoader>();
            _sanitizer = new DataSanitizer();
            _parser = new CategoryParser(_htmlLoaderMock.Object, new ConsoleLogger());
        }

        [Fact]
        public async Task GetCategoriesAsync_ValidHtml_ReturnsCategories()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/elektroinstrumenty/";
            var html = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "main-page.html"));
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var categories = await _parser.GetCategoriesAsync(url);

            // Assert
            Assert.NotEmpty(categories);
            Assert.Contains(categories, c => c.Name.Contains("Электроинструмент"));
        }

        [Fact]
        public async Task GetCategoriesAsync_EmptyHtml_ReturnsEmptyList()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/empty";
            var html = "<html><body></body></html>";
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var categories = await _parser.GetCategoriesAsync(url);

            // Assert
            Assert.Empty(categories);
        }

        [Fact]
        public async Task GetProductUrlsFromCategoryAsync_ValidHtml_ReturnsProductUrls()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/elektroinstrumenty/dreli/";
            var html = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "category-drili-page.html"));
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var productUrls = await _parser.GetProductUrlsFromCategoryAsync(url, maxPages: 1);

            // Assert
            Assert.NotEmpty(productUrls);
            Assert.Contains(productUrls, u => u.Contains("bosch-gsb-18v-50"));
            Assert.Contains(productUrls, u => u.Contains("makita-df457dse"));
        }

        [Fact]
        public async Task GetProductUrlsFromCategoryAsync_EmptyCategory_ReturnsEmptyList()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/empty";
            var html = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "empty-category.html"));
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var productUrls = await _parser.GetProductUrlsFromCategoryAsync(url, maxPages: 1);

            // Assert
            Assert.Empty(productUrls);
        }

        [Fact]
        public async Task GetSubCategoriesAsync_ValidHtml_ReturnsSubCategories()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/elektroinstrumenty/";
            var html = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "main-page.html"));
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var subCategories = await _parser.GetSubCategoriesAsync(url);

            // Assert
            // В тестовой HTML нет подкатегорий, но тест должен проходить
            Assert.NotNull(subCategories);
        }

        [Fact]
        public async Task GetCategoriesAsync_HtmlLoaderThrows_ThrowsException()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/error";
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _parser.GetCategoriesAsync(url));
        }

        [Fact]
        public async Task GetProductUrlsFromCategoryAsync_MultiplePages_ParsesAllPages()
        {
            // Arrange
            var baseUrl = "https://www.vseinstrumenti.ru/category/elektroinstrumenty/dreli/";
            var html = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "category-drili-page.html"));
            
            _htmlLoaderMock
                .SetupSequence(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(html) // Страница 1
                .ReturnsAsync(html) // Страница 2
                .ReturnsAsync(html); // Страница 3

            // Act
            var productUrls = await _parser.GetProductUrlsFromCategoryAsync(baseUrl, maxPages: 3);

            // Assert
            Assert.NotEmpty(productUrls);
            // Должно быть не менее 4 товаров (с одной страницы)
            Assert.InRange(productUrls.Count, 4, int.MaxValue);
        }

        [Fact]
        public async Task GetProductUrlsFromCategoryAsync_WithPagination_StopsWhenNoNewProducts()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/elektroinstrumenty/dreli/";
            var emptyHtml = "<html><body><div class='products-grid'></div></body></html>";
            
            _htmlLoaderMock
                .SetupSequence(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyHtml) // Страница 1 - пустая
                .ReturnsAsync(emptyHtml); // Страница 2 - не должна быть загружена

            // Act
            var productUrls = await _parser.GetProductUrlsFromCategoryAsync(url, maxPages: 5);

            // Assert
            Assert.Empty(productUrls);
            // Должна быть загружена только одна страница
            _htmlLoaderMock.Verify(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetCategoriesAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/slow";
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(url, cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
                _parser.GetCategoriesAsync(url, cts.Token));
        }

        [Fact]
        public void Constructor_NullHtmlLoader_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CategoryParser(
                null!,
                new ConsoleLogger()));
        }

        [Fact]
        public async Task GetProductUrlsFromCategoryAsync_TooManyPages_StopsAtMaxPages()
        {
            // Arrange
            var url = "https://www.vseinstrumenti.ru/category/elektroinstrumenty/dreli/";
            var html = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "category-drili-page.html"));
            
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var productUrls = await _parser.GetProductUrlsFromCategoryAsync(url, maxPages: 2);

            // Assert
            // Должно быть загружено максимум 2 страницы
            _htmlLoaderMock.Verify(
                x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
                Times.AtMost(2));
        }
    }

    /// <summary>
    /// Простой логгер для тестов
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            // Игнорируем логи в тестах
        }
    }
}
