using Moq;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class VoltProductParserTests : IDisposable
    {
        private readonly Mock<IHtmlLoader> _htmlLoaderMock;
        private readonly VoltProductParser _parser;
        private readonly string _testProductUrl = "https://220-volt.ru/product/drel-udarnaya-bosch-gsb-18v-50-123/";
        private readonly string _productFixturePath;
        private readonly string _emptyFixturePath;

        public VoltProductParserTests()
        {
            _htmlLoaderMock = new Mock<IHtmlLoader>();
            _parser = new VoltProductParser(_htmlLoaderMock.Object);
            
            _productFixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "product-page.html");
            _emptyFixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "error-page.html");
        }

        [Fact]
        public async Task ParseProductAsync_ValidHtml_ReturnsProductWithName()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(_testProductUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal("Дрель ударная Bosch GSB 18V-50 Professional", product.Name);
        }

        [Fact]
        public async Task ParseProductAsync_ValidHtml_ReturnsCorrectPrice()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(_testProductUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal(8490m, product.Price);
        }

        [Fact]
        public async Task ParseProductAsync_ValidHtml_ReturnsCorrectBrand()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(_testProductUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal("Bosch", product.Brand);
        }

        [Fact]
        public async Task ParseProductAsync_ValidHtml_ReturnsCorrectArticle()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(_testProductUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal("06019H6100", product.Article);
        }

        [Fact]
        public async Task ParseProductAsync_ValidHtml_ReturnsInStockAvailability()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(_testProductUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal(AvailabilityStatus.InStock, product.Availability);
        }

        [Fact]
        public async Task ParseProductAsync_ValidHtml_ReturnsSpecifications()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(_testProductUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.NotEmpty(product.Specifications);
            Assert.Contains("Напряжение", product.Specifications.Keys);
            Assert.Equal("18 В", product.Specifications["Напряжение"]);
        }

        [Fact]
        public async Task ParseProductAsync_EmptyHtml_ReturnsDefaultValues()
        {
            // Arrange
            var html = File.ReadAllText(_emptyFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync("https://example.com/invalid");

            // Assert
            Assert.NotNull(product);
            Assert.Equal("Неизвестно", product.Name);
            Assert.Equal("Неизвестно", product.Brand);
        }

        [Fact]
        public async Task ParseProductAsync_HtmlLoaderThrows_ThrowsException()
        {
            // Arrange
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Connection timeout"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _parser.ParseProductAsync(_testProductUrl));
        }

        [Fact]
        public async Task ParseProductAsync_PartialPrice_ReturnsPriceOnly()
        {
            // Arrange
            var html = @"
<!DOCTYPE html>
<html>
<body>
    <h1 class='product-title'>Дрель Bosch</h1>
    <div class='price-current'>15 200 ₽</div>
</body>
</html>";
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);

            // Act
            var product = await _parser.ParseProductAsync(_testProductUrl);

            // Assert
            Assert.NotNull(product);
            Assert.Equal(15200m, product.Price);
        }

        [Fact]
        public async Task ParseProductAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var html = File.ReadAllText(_productFixturePath);
            _htmlLoaderMock
                .Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(html);
            
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
                _parser.ParseProductAsync(_testProductUrl, cts.Token));
        }

        public void Dispose()
        {
            _htmlLoaderMock?.VerifyAll();
        }
    }
}
