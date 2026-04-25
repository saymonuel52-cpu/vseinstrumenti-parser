using Xunit;
using Moq;
using VseinstrumentiParser.Services.Caching;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class CacheServiceTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly ParsingLimits _parsingLimits;

        public CacheServiceTests()
        {
            _loggerMock = new Mock<ILogger>();
            _parsingLimits = new ParsingLimits
            {
                EnableCaching = true,
                CacheDurationMinutes = 1
            };
        }

        [Fact]
        public void CacheService_Constructor_InitializesCorrectly()
        {
            // Arrange & Act
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);

            // Assert
            Assert.NotNull(cacheService);
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Сервис кэширования инициализирован"))), Times.Once);
        }

        [Fact]
        public void SetAndGet_WithValidData_ReturnsCachedValue()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);
            var testData = new List<string> { "item1", "item2", "item3" };

            // Act
            cacheService.Set("test_key", testData);
            var retrieved = cacheService.Get<List<string>>("test_key");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(3, retrieved.Count);
            Assert.Equal("item1", retrieved[0]);
            Assert.Equal("item2", retrieved[1]);
            Assert.Equal("item3", retrieved[2]);
        }

        [Fact]
        public void Get_WithNonExistentKey_ReturnsDefault()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);

            // Act
            var result = cacheService.Get<List<string>>("non_existent_key");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Get_WithExpiredItem_ReturnsDefault()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);
            var testData = "test data";
            
            // Используем очень короткое время жизни кэша
            var shortLivedSettings = new ParsingLimits
            {
                EnableCaching = true,
                CacheDurationMinutes = 0 // 0 минут = мгновенное истечение
            };
            
            var shortLivedCache = new CacheService(_loggerMock.Object, shortLivedSettings);
            shortLivedCache.Set("expired_key", testData, TimeSpan.FromMilliseconds(1));

            // Ждем немного, чтобы кэш истек
            Thread.Sleep(10);

            // Act
            var result = shortLivedCache.Get<string>("expired_key");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void CreateCategoriesKey_ReturnsConsistentFormat()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);
            var site = "vseinstrumenti";

            // Act
            var key = cacheService.CreateCategoriesKey(site);

            // Assert
            Assert.StartsWith("categories_vseinstrumenti_", key);
            Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), key);
        }

        [Fact]
        public void CreateProductsKey_ReturnsConsistentFormat()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);
            var categoryUrl = "https://example.com/category";
            var maxPages = 5;

            // Act
            var key = cacheService.CreateProductsKey(categoryUrl, maxPages);

            // Assert
            Assert.StartsWith("products_", key);
            Assert.Contains("_5_", key);
            Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), key);
        }

        [Fact]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);
            cacheService.Set("key1", "value1");
            cacheService.Set("key2", "value2");
            cacheService.Set("key3", "value3");

            // Act
            cacheService.Clear();

            // Assert
            Assert.Null(cacheService.Get<string>("key1"));
            Assert.Null(cacheService.Get<string>("key2"));
            Assert.Null(cacheService.Get<string>("key3"));
            
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Кэш очищен"))), Times.Once);
        }

        [Fact]
        public void GetStatistics_ReturnsCorrectCounts()
        {
            // Arrange
            var cacheService = new CacheService(_loggerMock.Object, _parsingLimits);
            cacheService.Set("key1", "value1");
            cacheService.Set("key2", "value2");

            // Act
            var stats = cacheService.GetStatistics();

            // Assert
            Assert.Equal(2, stats.TotalItems);
            Assert.Equal(2, stats.ValidItems);
            Assert.Equal(0, stats.ExpiredItems);
        }

        [Fact]
        public void CacheDisabled_GetReturnsDefault()
        {
            // Arrange
            var disabledSettings = new ParsingLimits
            {
                EnableCaching = false,
                CacheDurationMinutes = 60
            };
            
            var cacheService = new CacheService(_loggerMock.Object, disabledSettings);
            cacheService.Set("key1", "value1");

            // Act
            var result = cacheService.Get<string>("key1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void CacheDisabled_SetDoesNothing()
        {
            // Arrange
            var disabledSettings = new ParsingLimits
            {
                EnableCaching = false,
                CacheDurationMinutes = 60
            };
            
            var cacheService = new CacheService(_loggerMock.Object, disabledSettings);

            // Act (не должно быть исключений)
            var exception = Record.Exception(() => cacheService.Set("key1", "value1"));

            // Assert
            Assert.Null(exception);
        }
    }
}