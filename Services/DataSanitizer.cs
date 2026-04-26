using System.Text.RegularExpressions;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Сервис для очистки и нормализации извлечённых данных
    /// Отделяет логику очистки от логики парсинга
    /// </summary>
    public class DataSanitizer
    {
        private static readonly Regex MultipleSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex NonBreakingSpaceRegex = new Regex(@"\u00A0", RegexOptions.Compiled);
        private static readonly Regex CurrencySymbolsRegex = new Regex(@"[₽\$€£]", RegexOptions.Compiled);
        private static readonly Regex WhitespaceCharsRegex = new Regex(@"[\t\n\r\f\v]", RegexOptions.Compiled);

        /// <summary>
        /// Очистить текст от лишних пробелов, переносов и спецсимволов
        /// </summary>
        public string CleanText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Заменяем неразрывные пробелы на обычные
            text = NonBreakingSpaceRegex.Replace(text, " ");

            // Убираем лишние пробелы между словами
            text = MultipleSpacesRegex.Replace(text, " ");

            // Убираем управляющие символы
            text = WhitespaceCharsRegex.Replace(text, "");

            // Обрезаем и возвращаем
            return text.Trim();
        }

        /// <summary>
        /// Очистить название товара
        /// </summary>
        public string CleanProductName(string? text)
        {
            var cleaned = CleanText(text);

            // Убираем "Купить" и подобные слова из начала
            cleaned = Regex.Replace(cleaned, @"^(купить|купить онлайн|цена на|интернет-магазин)\s+", "", RegexOptions.IgnoreCase);

            // Обрезаем длинные названия (больше 200 символов)
            if (cleaned.Length > 200)
            {
                cleaned = cleaned.Substring(0, 197) + "...";
            }

            return cleaned;
        }

        /// <summary>
        /// Очистить и распарсить цену
        /// </summary>
        public bool TryParsePrice(string? text, out decimal price)
        {
            price = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Удаляем валюту и пробелы
            var cleaned = CurrencySymbolsRegex.Replace(text, "");
            cleaned = cleaned.Replace(" ", "").Replace(" ", ""); // обычный и неразрывный пробел

            // Заменяем запятую на точку
            cleaned = cleaned.Replace(",", ".");

            // Удаляем всё, кроме цифр и точки
            cleaned = Regex.Replace(cleaned, @"[^\d\.]", "");

            // Обрабатываем несколько точек (разделители тысяч)
            if (cleaned.Count(c => c == '.') > 1)
            {
                var lastDotIndex = cleaned.LastIndexOf('.');
                cleaned = cleaned.Remove(lastDotIndex, 1);
            }

            return decimal.TryParse(cleaned, out price);
        }

        /// <summary>
        /// Очистить бренд
        /// </summary>
        public string CleanBrand(string? text)
        {
            var cleaned = CleanText(text);

            if (string.IsNullOrEmpty(cleaned))
                return "Неизвестно";

            // Приводим к нормальному регистру (первая буква заглавная)
            if (cleaned.Length > 0)
            {
                cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1).ToLowerInvariant();
            }

            return cleaned;
        }

        /// <summary>
        /// Очистить артикул
        /// </summary>
        public string CleanArticle(string? text)
        {
            var cleaned = CleanText(text);

            if (string.IsNullOrEmpty(cleaned))
                return string.Empty;

            // Артикулы обычно не длиннее 50 символов
            return cleaned.Length > 50 ? cleaned.Substring(0, 50) : cleaned;
        }

        /// <summary>
        /// Нормализовать ключ характеристики
        /// </summary>
        public string NormalizeSpecificationKey(string key)
        {
            var cleaned = CleanText(key).ToLowerInvariant();

            // Маппинг синонимов
            var synonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "мощность", "Мощность" },
                { "тип двигателя", "Тип двигателя" },
                { "двигатель", "Тип двигателя" },
                { "напряжение", "Напряжение" },
                { "voltage", "Напряжение" },
                { "скорость", "Скорость" },
                { "обороты", "Скорость" },
                { "rpm", "Скорость" },
                { "об/мин", "Скорость" },
                { "вес", "Вес" },
                { "weight", "Вес" },
                { "масса", "Вес" },
                { "глубина реза", "Глубина реза" },
                { "глубина резания", "Глубина реза" },
                { "диаметр сверла", "Диаметр сверла" },
                { "патрон", "Тип патрона" },
                { "chuck", "Тип патрона" },
                { "тип аккумулятора", "Тип аккумулятора" },
                { "аккумулятор", "Тип аккумулятора" },
                { "емкость акк", "Ёмкость аккумулятора" },
                { "ёмкость", "Ёмкость аккумулятора" },
            };

            if (synonyms.TryGetValue(cleaned, out var normalized))
            {
                return normalized;
            }

            // Возвращаем с корректным регистром
            return cleaned.ToTitleCaseInvariant();
        }

        /// <summary>
        /// Очистить URL
        /// </summary>
        public string? CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            url = url.Trim();

            // Убираем якоря и query параметры
            var questionMarkIndex = url.IndexOf('?');
            if (questionMarkIndex >= 0)
            {
                url = url.Substring(0, questionMarkIndex);
            }

            var hashIndex = url.IndexOf('#');
            if (hashIndex >= 0)
            {
                url = url.Substring(0, hashIndex);
            }

            return url;
        }

        /// <summary>
        /// Проверить, является ли текст HTML-тегом
        /// </summary>
        public bool IsHtmlTag(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(text.Trim(), @"^<[^>]+>$");
        }

        /// <summary>
        /// Удалить все HTML-теги из текста
        /// </summary>
        public string StripHtmlTags(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return Regex.Replace(text, @"<[^>]+>", "");
        }
    }

    /// <summary>
    /// Расширение для TitleCase в инвариантной культуре
    /// </summary>
    internal static class StringExtensions
    {
        public static string ToTitleCaseInvariant(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var cultureInfo = System.Globalization.CultureInfo.InvariantCulture;
            var textInfo = cultureInfo.TextInfo;
            return textInfo.ToTitleCase(str.ToLowerInvariant());
        }
    }
}
