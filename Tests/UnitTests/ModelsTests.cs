using Xunit;
using VseinstrumentiParser.Models;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class ModelsTests
    {
        [Fact]
        public void Category_Constructor_SetsProperties()
        {
            // Arrange & Act
            var category = new Category
            {
                Name = "Дрели",
                Url = "https://example.com/drills",
                ProductCount = 150
            };

            // Assert
            Assert.Equal("Дрели", category.Name);
            Assert.Equal("https://example.com/drills", category.Url);
            Assert.Equal(150, category.ProductCount);
        }

        [Fact]
        public void Category_ToString_ReturnsName()
        {
            // Arrange
            var category = new Category { Name = "Шуруповерты" };

            // Act & Assert
            Assert.Equal("Шуруповерты", category.ToString());
        }

        [Fact]
        public void Product_Constructor_SetsProperties()
        {
            // Arrange & Act
            var product = new Product
            {
                Name = "Дрель Bosch GSB 1200",
                Brand = "Bosch",
                Price = 3499.99m,
                Currency = "руб.",
                Availability = "В наличии",
                Url = "https://example.com/product",
                Category = "Дрели",
                Description = "Мощная дрель для домашнего использования",
                Rating = 4.5f,
                ReviewCount = 42
            };

            // Assert
            Assert.Equal("Дрель Bosch GSB 1200", product.Name);
            Assert.Equal("Bosch", product.Brand);
            Assert.Equal(3499.99m, product.Price);
            Assert.Equal("руб.", product.Currency);
            Assert.Equal("В наличии", product.Availability);
            Assert.Equal("https://example.com/product", product.Url);
            Assert.Equal("Дрели", product.Category);
            Assert.Equal("Мощная дрель для домашнего использования", product.Description);
            Assert.Equal(4.5f, product.Rating);
            Assert.Equal(42, product.ReviewCount);
        }

        [Fact]
        public void Product_ToString_ReturnsNameAndPrice()
        {
            // Arrange
            var product = new Product
            {
                Name = "Дрель Bosch",
                Price = 3499.99m,
                Currency = "руб."
            };

            // Act & Assert
            Assert.Contains("Дрель Bosch", product.ToString());
            Assert.Contains("3499.99", product.ToString());
        }

        [Fact]
        public void Product_PriceFormatted_ReturnsCorrectFormat()
        {
            // Arrange
            var product = new Product
            {
                Price = 3499.99m,
                Currency = "руб."
            };

            // Act
            var formatted = product.PriceFormatted;

            // Assert
            Assert.Equal("3 499,99 руб.", formatted);
        }

        [Fact]
        public void Product_IsAvailable_ReturnsTrueForInStock()
        {
            // Arrange
            var product1 = new Product { Availability = "В наличии" };
            var product2 = new Product { Availability = "Под заказ" };
            var product3 = new Product { Availability = "Нет в наличии" };

            // Act & Assert
            Assert.True(product1.IsAvailable);
            Assert.False(product2.IsAvailable);
            Assert.False(product3.IsAvailable);
        }
    }
}