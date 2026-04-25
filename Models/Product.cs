namespace VseinstrumentiParser.Models
{
    /// <summary>
    /// Модель товара (электроинструмента)
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Название товара
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL страницы товара
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Цена в рублях
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// Старая цена (если есть скидка)
        /// </summary>
        public decimal? OldPrice { get; set; }

        /// <summary>
        /// Бренд/производитель
        /// </summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// Артикул товара
        /// </summary>
        public string Article { get; set; } = string.Empty;

        /// <summary>
        /// Наличие товара
        /// </summary>
        public AvailabilityStatus Availability { get; set; } = AvailabilityStatus.Unknown;

        /// <summary>
        /// Детальное описание наличия (например, "В наличии 5 шт.")
        /// </summary>
        public string AvailabilityDetails { get; set; } = string.Empty;

        /// <summary>
        /// Характеристики товара (ключ-значение)
        /// </summary>
        public Dictionary<string, string> Specifications { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Рейтинг товара (от 1 до 5)
        /// </summary>
        public double? Rating { get; set; }

        /// <summary>
        /// Количество отзывов
        /// </summary>
        public int? ReviewCount { get; set; }

        /// <summary>
        /// Категория товара
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Дата парсинга
        /// </summary>
        public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            return $"{Name} - {Price} руб. ({Brand})";
        }
    }

    /// <summary>
    /// Статус наличия товара
    /// </summary>
    public enum AvailabilityStatus
    {
        Unknown,
        InStock,
        OutOfStock,
        Limited,
        PreOrder,
        NotAvailable
    }
}