namespace VseinstrumentiParser.Models
{
    /// <summary>
    /// Модель категории электроинструментов
    /// </summary>
    public class Category
    {
        /// <summary>
        /// Название категории
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL страницы категории
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Количество товаров в категории (если доступно)
        /// </summary>
        public int? ProductCount { get; set; }

        /// <summary>
        /// Дочерние подкатегории
        /// </summary>
        public List<Category> SubCategories { get; set; } = new List<Category>();

        public override string ToString()
        {
            return $"{Name} ({Url})";
        }
    }
}