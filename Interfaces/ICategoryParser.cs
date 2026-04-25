using VseinstrumentiParser.Models;

namespace VseinstrumentiParser.Interfaces
{
    /// <summary>
    /// Интерфейс для парсинга категорий электроинструментов
    /// </summary>
    public interface ICategoryParser
    {
        /// <summary>
        /// Получить все основные категории электроинструментов
        /// </summary>
        /// <param name="baseUrl">Базовый URL сайта (по умолчанию https://www.vseinstrumenti.ru)</param>
        /// <returns>Список категорий</returns>
        Task<List<Category>> GetCategoriesAsync(string baseUrl = "https://www.vseinstrumenti.ru");

        /// <summary>
        /// Получить подкатегории для указанной категории
        /// </summary>
        /// <param name="categoryUrl">URL категории</param>
        /// <returns>Список подкатегорий</returns>
        Task<List<Category>> GetSubCategoriesAsync(string categoryUrl);

        /// <summary>
        /// Получить все товары из категории (пагинация)
        /// </summary>
        /// <param name="categoryUrl">URL категории</param>
        /// <param name="maxPages">Максимальное количество страниц для парсинга</param>
        /// <returns>Список URL товаров</returns>
        Task<List<string>> GetProductUrlsFromCategoryAsync(string categoryUrl, int maxPages = 10);
    }
}