using VseinstrumentiParser.Models;

namespace VseinstrumentiParser.Interfaces
{
    /// <summary>
    /// Интерфейс для парсинга информации о товаре
    /// </summary>
    public interface IProductParser
    {
        /// <summary>
        /// Получить детальную информацию о товаре по его URL
        /// </summary>
        /// <param name="productUrl">URL страницы товара</param>
        /// <returns>Объект Product с заполненными данными</returns>
        Task<Product> ParseProductAsync(string productUrl);

        /// <summary>
        /// Пакетный парсинг нескольких товаров
        /// </summary>
        /// <param name="productUrls">Список URL товаров</param>
        /// <param name="maxConcurrent">Максимальное количество одновременных запросов</param>
        /// <returns>Список товаров</returns>
        Task<List<Product>> ParseProductsAsync(IEnumerable<string> productUrls, int maxConcurrent = 5);
    }
}