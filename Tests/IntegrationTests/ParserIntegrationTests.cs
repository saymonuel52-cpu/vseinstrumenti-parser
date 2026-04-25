using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using Moq;
using VseinstrumentiParser.Models;
using VseinstrumentiParser.Services;
using VseinstrumentiParser.Services.Http;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Tests.IntegrationTests
{
    public class ParserIntegrationTests : IAsyncLifetime
    {
        private WireMockServer _mockServer;
        private readonly Mock<ILogger> _loggerMock;
        private readonly RequestSettings _requestSettings;
        private string _mockServerUrl;

        public ParserIntegrationTests()
        {
            _loggerMock = new Mock<ILogger>();
            _requestSettings = new RequestSettings
            {
                TimeoutSeconds = 5,
                MaxRetries = 2,
                RetryDelayMs = 100,
                DelayBetweenRequestsMs = 0,
                UserAgent = "TestAgent"
            };
        }

        public async Task InitializeAsync()
        {
            // Запускаем WireMock сервер
            _mockServer = WireMockServer.Start();
            _mockServerUrl = _mockServer.Url;
            
            // Настраиваем моки для тестов
            SetupMockResponses();
            
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            // Останавливаем WireMock сервер
            _mockServer?.Stop();
            _mockServer?.Dispose();
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Настройка мок-ответов WireMock
        /// </summary>
        private void SetupMockResponses()
        {
            // Мок главной страницы с категориями
            _mockServer
                .Given(Request.Create().WithPath("/categories").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "text/html")
                    .WithBody(GetMockCategoriesHtml()));

            // Мок страницы категории с товарами
            _mockServer
                .Given(Request.Create().WithPath("/category/drills").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "text/html")
                    .WithBody(GetMockCategoryHtml()));

            // Мок страницы товара
            _mockServer
                .Given(Request.Create().WithPath("/product/123").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "text/html")
                    .WithBody(GetMockProductHtml()));

            // Мок для тестирования ошибок
            _mockServer
                .Given(Request.Create().WithPath("/error").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(500)
                    .WithBody("Internal Server Error"));

            // Мок для тестирования 429 Too Many Requests
            _mockServer
                .Given(Request.Create().WithPath("/rate-limit").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(429)
                    .WithHeader("Retry-After", "1")
                    .WithBody("Too Many Requests"));

            // Мок для тестирования retry логики
            var callCount = 0;
            _mockServer
                .Given(Request.Create().WithPath("/retry-test").UsingGet())
                .RespondWith(Response.Create()
                    .WithTransformer()
                    .WithStatusCode(context =>
                    {
                        callCount++;
                        return callCount <= 2 ? 500 : 200;
                    })
                    .WithBody(context => callCount <= 2 ? "Error" : "Success"));
        }

        [Fact]
        public async Task HttpClientService_GetHtmlContentAsync_ReturnsHtmlContent()
        {
            // Arrange
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                _requestSettings);

            // Act
            var html = await httpClientService.GetHtmlContentAsync($"{_mockServerUrl}/categories");

            // Assert
            Assert.NotNull(html);
            Assert.Contains("Дрели", html);
            Assert.Contains("Шуруповерты", html);
            
            _loggerMock.Verify(l => l.LogDebug(It.Is<string>(s => s.Contains("Запрос:"))), Times.Once);
            _loggerMock.Verify(l => l.LogDebug(It.Is<string>(s => s.Contains("Ответ получен:"))), Times.Once);
        }

        [Fact]
        public async Task HttpClientService_GetHtmlContentAsync_HandlesHttpErrors()
        {
            // Arrange
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                _requestSettings);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                httpClientService.GetHtmlContentAsync($"{_mockServerUrl}/error"));
            
            _loggerMock.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Ошибка при запросе"))), Times.Once);
        }

        [Fact]
        public async Task HttpClientService_GetHtmlContentAsync_RetriesOnFailure()
        {
            // Arrange
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                _requestSettings);

            // Act
            var html = await httpClientService.GetHtmlContentAsync($"{_mockServerUrl}/retry-test");

            // Assert
            Assert.Equal("Success", html);
            
            // Проверяем, что были логи о retry
            _loggerMock.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Повторная попытка"))), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CategoryParser_ParseCategories_ReturnsCategories()
        {
            // Arrange
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                _requestSettings);
            
            var categoryParser = new CategoryParser(httpClientService, _loggerMock.Object);

            // Act
            var categories = await categoryParser.GetCategoriesAsync($"{_mockServerUrl}/categories");

            // Assert
            Assert.NotNull(categories);
            Assert.Equal(2, categories.Count);
            
            var drillCategory = categories.First(c => c.Name == "Дрели");
            Assert.Equal($"{_mockServerUrl}/category/drills", drillCategory.Url);
            Assert.Equal(150, drillCategory.ProductCount);
            
            var screwdriverCategory = categories.First(c => c.Name == "Шуруповерты");
            Assert.Equal($"{_mockServerUrl}/category/screwdrivers", screwdriverCategory.Url);
            Assert.Equal(89, screwdriverCategory.ProductCount);
        }

        [Fact]
        public async Task ProductParser_ParseProduct_ReturnsProductDetails()
        {
            // Arrange
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                _requestSettings);
            
            var productParser = new ProductParser(httpClientService, _loggerMock.Object);
            var productUrl = $"{_mockServerUrl}/product/123";

            // Act
            var product = await productParser.ParseProductAsync(productUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal("Дрель Bosch GSB 1200", product.Name);
            Assert.Equal("Bosch", product.Brand);
            Assert.Equal(3499.99m, product.Price);
            Assert.Equal("руб.", product.Currency);
            Assert.Equal("В наличии", product.Availability);
            Assert.Equal(productUrl, product.Url);
            Assert.Equal("Дрели", product.Category);
            Assert.Contains("Мощная дрель для домашнего использования", product.Description);
            Assert.Equal(4.5f, product.Rating);
            Assert.Equal(42, product.ReviewCount);
        }

        [Fact]
        public async Task ParserService_GetProductsFromCategory_ReturnsProducts()
        {
            // Arrange
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                _requestSettings);
            
            var categoryParser = new CategoryParser(httpClientService, _loggerMock.Object);
            var productParser = new ProductParser(httpClientService, _loggerMock.Object);
            
            var parserService = new VseinstrumentiParserServiceWithCache(
                _loggerMock.Object,
                parsingLimits: new ParsingLimits { EnableCaching = false });

            // Для теста подменим зависимости через reflection
            SetPrivateField(parserService, "_httpClient", httpClientService);
            SetPrivateField(parserService, "_categoryParser", categoryParser);
            SetPrivateField(parserService, "_productParser", productParser);

            // Act
            var products = await parserService.GetProductsFromCategoryAsync(
                $"{_mockServerUrl}/category/drills",
                maxPages: 1,
                useCache: false);

            // Assert
            Assert.NotNull(products);
            Assert.Single(products);
            
            var product = products.First();
            Assert.Equal("Дрель Bosch GSB 1200", product.Name);
            Assert.Equal(3499.99m, product.Price);
        }

        [Fact]
        public async Task RateLimiting_TooManyRequests_TriggersRetry()
        {
            // Arrange
            var settingsWithRetry = new RequestSettings
            {
                TimeoutSeconds = 5,
                MaxRetries = 3,
                RetryDelayMs = 100,
                DelayBetweenRequestsMs = 0,
                UserAgent = "TestAgent"
            };
            
            var httpClientFactory = CreateMockHttpClientFactory();
            var httpClientService = new ResilientHttpClientService(
                httpClientFactory.Object,
                _loggerMock.Object,
                settingsWithRetry);

            // Act & Assert
            // WireMock настроен возвращать 429 на /rate-limit
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                httpClientService.GetHtmlContentAsync($"{_mockServerUrl}/rate-limit"));
            
            // Проверяем, что была попытка retry
            _loggerMock.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Повторная попытка"))), Times.AtLeastOnce);
        }

        /// <summary>
        /// Создание мок IHttpClientFactory
        /// </summary>
        private Mock<IHttpClientFactory> CreateMockHttpClientFactory()
        {
            var mockFactory = new Mock<IHttpClientFactory>();
            var clientHandler = new HttpClientHandler();
            var client = new HttpClient(clientHandler)
            {
                BaseAddress = new Uri(_mockServerUrl)
            };
            
            mockFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(client);
            
            return mockFactory;
        }

        /// <summary>
        /// Установка приватного поля через reflection (для тестов)
        /// </summary>
        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        /// <summary>
        /// HTML мок главной страницы с категориями
        /// </summary>
        private string GetMockCategoriesHtml()
        {
            return @"
                <html>
                    <body>
                        <div class='categories'>
                            <div class='category'>
                                <a href='/category/drills'>Дрели</a>
                                <span class='count'>(150 товаров)</span>
                            </div>
                            <div class='category'>
                                <a href='/category/screwdrivers'>Шуруповерты</a>
                                <span class='count'>(89 товаров)</span>
                            </div>
                        </div>
                    </body>
                </html>";
        }

        /// <summary>
        /// HTML мок страницы категории
        /// </summary>
        private string GetMockCategoryHtml()
        {
            return @"
                <html>
                    <body>
                        <div class='products'>
                            <div class='product'>
                                <a href='/product/123' class='product-link'>Дрель Bosch GSB 1200</a>
                                <div class='price'>3 499,99 руб.</div>
                            </div>
                        </div>
                    </body>
                </html>";
        }

        /// <summary>
        /// HTML мок страницы товара
        /// </summary>
        private string GetMockProductHtml()
        {
            return @"
                <html>
                    <body>
                        <h1 class='product-title'>Дрель Bosch GSB 1200</h1>
                        <div class='product-brand'>Бренд: Bosch</div>
                        <div class='product-price'>3 499,99 <span class='currency'>руб.</span></div>
                        <div class='product-availability'>В наличии</div>
                        <div class='product-category'>Категория: <a href='/category/drills'>Дрели</a></div>
                        <div class='product-description'>
                            <p>Мощная дрель для домашнего использования. Мощность 1200 Вт.</p>
                        </div>
                        <div class='product-rating'>
                            <span class='rating-value'>4.5</span>
                            <span class='review-count'>(42 отзыва)</span>
                        </div>
                    </body>
                </html>";
        }
    }
}