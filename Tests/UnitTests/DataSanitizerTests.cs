using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class DataSanitizerTests
    {
        private readonly DataSanitizer _sanitizer;

        public DataSanitizerTests()
        {
            _sanitizer = new DataSanitizer();
        }

        [Fact]
        public void CleanText_RemovesMultipleSpaces_ReturnsCleanText()
        {
            // Arrange
            var input = "Дрель    ударная    Bosch";

            // Act
            var result = _sanitizer.CleanText(input);

            // Assert
            Assert.Equal("Дрель ударная Bosch", result);
        }

        [Fact]
        public void CleanText_RemovesNewLines_ReturnsSingleLine()
        {
            // Arrange
            var input = "Дрель\nударная\tBosch\r\nProfessional";

            // Act
            var result = _sanitizer.CleanText(input);

            // Assert
            Assert.Equal("Дрель ударная Bosch Professional", result);
        }

        [Fact]
        public void CleanText_HandlesNull_ReturnsEmpty()
        {
            // Arrange
            string? input = null;

            // Act
            var result = _sanitizer.CleanText(input);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void CleanProductName_RemovesBuyPrefix_ReturnsCleanName()
        {
            // Arrange
            var input = "Купить Дрель ударная Bosch GSB 18V";

            // Act
            var result = _sanitizer.CleanProductName(input);

            // Assert
            Assert.Equal("Дрель ударная Bosch GSB 18V", result);
        }

        [Fact]
        public void CleanProductName_HandlesEmpty_ReturnsEmpty()
        {
            // Arrange
            var input = "";

            // Act
            var result = _sanitizer.CleanProductName(input);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void TryParsePrice_WithRubles_ReturnsCorrectPrice()
        {
            // Arrange
            var input = "8 490 ₽";

            // Act
            var result = _sanitizer.TryParsePrice(input, out var price);

            // Assert
            Assert.True(result);
            Assert.Equal(8490m, price);
        }

        [Fact]
        public void TryParsePrice_WithComma_ReturnsCorrectPrice()
        {
            // Arrange
            var input = "12 350,50";

            // Act
            var result = _sanitizer.TryParsePrice(input, out var price);

            // Assert
            Assert.True(result);
            Assert.Equal(12350.50m, price);
        }

        [Fact]
        public void TryParsePrice_WithInvalidInput_ReturnsFalse()
        {
            // Arrange
            var input = "не цена";

            // Act
            var result = _sanitizer.TryParsePrice(input, out var price);

            // Assert
            Assert.False(result);
            Assert.Equal(0m, price);
        }

        [Fact]
        public void CleanBrand_NormalizesCase_ReturnsProperCase()
        {
            // Arrange
            var input = "bosch";

            // Act
            var result = _sanitizer.CleanBrand(input);

            // Assert
            Assert.Equal("Bosch", result);
        }

        [Fact]
        public void CleanArticle_CleansWhitespace_ReturnsCleanArticle()
        {
            // Arrange
            var input = "  06019H6100  ";

            // Act
            var result = _sanitizer.CleanArticle(input);

            // Assert
            Assert.Equal("06019H6100", result);
        }

        [Fact]
        public void NormalizeSpecificationKey_MapsSynonyms_ReturnsNormalizedKey()
        {
            // Arrange
            var input = "двигатель";

            // Act
            var result = _sanitizer.NormalizeSpecificationKey(input);

            // Assert
            Assert.Equal("Тип двигателя", result);
        }

        [Fact]
        public void NormalizeSpecificationKey_Voltage_ReturnsCorrect()
        {
            // Arrange
            var input = "voltage";

            // Act
            var result = _sanitizer.NormalizeSpecificationKey(input);

            // Assert
            Assert.Equal("Напряжение", result);
        }

        [Fact]
        public void StripHtmlTags_RemovesTags_ReturnsPlainText()
        {
            // Arrange
            var input = "<span>Дрель</span> <b>Bosch</b>";

            // Act
            var result = _sanitizer.StripHtmlTags(input);

            // Assert
            Assert.Equal("Дрель Bosch", result);
        }

        [Fact]
        public void CleanUrl_RemovesQueryParams_ReturnsBase()
        {
            // Arrange
            var input = "https://example.com/product/123?page=2&sort=price";

            // Act
            var result = _sanitizer.CleanUrl(input);

            // Assert
            Assert.Equal("https://example.com/product/123", result);
        }

        [Fact]
        public void CleanUrl_HandlesNull_ReturnsNull()
        {
            // Arrange
            string? input = null;

            // Act
            var result = _sanitizer.CleanUrl(input);

            // Assert
            Assert.Null(result);
        }
    }
}
