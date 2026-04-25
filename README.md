# Парсер каталога электроинструментов vseinstrumenti.ru

C# сервис для парсинга каталога электроинструментов с сайта vseinstrumenti.ru.

## Особенности

- **Асинхронный парсинг** с использованием HttpClient
- **Поддержка Cookie** (автоматическое получение через предварительный GET-запрос)
- **Правильный User-Agent** для обхода блокировок
- **Два интерфейса**: `ICategoryParser` и `IProductParser`
- **Обработка ошибок** с экспоненциальной задержкой (exponential backoff)
- **Логирование** в консоль с временными метками
- **Пакетный парсинг** товаров с ограничением параллелизма
- **Экспорт в CSV** для дальнейшего анализа

## Структура проекта

```
VseinstrumentiParser/
├── Interfaces/
│   ├── ICategoryParser.cs      # Интерфейс парсера категорий
│   └── IProductParser.cs       # Интерфейс парсера товаров
├── Models/
│   ├── Category.cs             # Модель категории
│   └── Product.cs              # Модель товара
├── Services/
│   ├── HttpClientService.cs    # Сервис HTTP с поддержкой Cookie
│   ├── CategoryParser.cs       # Реализация парсера категорий
│   ├── ProductParser.cs        # Реализация парсера товаров
│   └── VseinstrumentiParserService.cs # Основной сервис
├── Utilities/
│   └── RetryPolicy.cs          # Политика повторных попыток
├── Program.cs                  # Пример использования
└── VseinstrumentiParser.csproj # Файл проекта
```

## Требования

- .NET 8.0 или выше
- NuGet пакеты:
  - AngleSharp (для парсинга HTML)
  - Microsoft.Extensions.Logging (опционально)

## Установка

1. Клонируйте репозиторий
2. Восстановите зависимости:
   ```bash
   dotnet restore
   ```
3. Соберите проект:
   ```bash
   dotnet build
   ```

## Использование

### Базовый пример

```csharp
using VseinstrumentiParser.Services;

// Создание сервиса
using var parserService = new VseinstrumentiParserService();

// Получение категорий
var categories = await parserService.GetCategoriesAsync();

// Парсинг товаров из категории
var products = await parserService.GetProductsFromCategoryAsync(
    "https://www.vseinstrumenti.ru/category/elektroinstrumenty/dreli/", 
    maxPages: 2
);

// Экспорт в CSV
await parserService.ExportToCsvAsync(products, "products.csv");
```

### Расширенный пример

```csharp
// Полный парсинг каталога
var allProductsByCategory = await parserService.GetAllProductsAsync(
    maxProductsPerCategory: 50
);

// Парсинг конкретного товара
var productDetails = await parserService.GetProductDetailsAsync(
    "https://www.vseinstrumenti.ru/product/drel-udarnaya-bosch-gsb-13-re-0-601-9a0-100-723663/"
);
```

## Интерфейсы

### ICategoryParser
```csharp
public interface ICategoryParser
{
    Task<List<Category>> GetCategoriesAsync(string baseUrl = "https://www.vseinstrumenti.ru");
    Task<List<Category>> GetSubCategoriesAsync(string categoryUrl);
    Task<List<string>> GetProductUrlsFromCategoryAsync(string categoryUrl, int maxPages = 10);
}
```

### IProductParser
```csharp
public interface IProductParser
{
    Task<Product> ParseProductAsync(string productUrl);
    Task<List<Product>> ParseProductsAsync(IEnumerable<string> productUrls, int maxConcurrent = 5);
}
```

## Обработка ошибок

Сервис включает встроенную политику повторных попыток с экспоненциальной задержкой:
- Начальная задержка: 1 секунда
- Множитель: 2.0
- Максимальное количество попыток: 5
- Максимальная задержка: 30 секунд

Ошибки логируются в консоль с временными метками:
```
[14:30:25] GET https://www.vseinstrumenti.ru/ (попытка 1/3)
[14:30:26] Успешно получено 24567 байт
[14:30:27] Ошибка: Connection refused. Повтор через 2000мс
```

## Настройка

### User-Agent
По умолчанию используется:
```
Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36
```

Можно изменить в конструкторе `HttpClientService`.

### Параллелизм
По умолчанию используется до 5 одновременных запросов при пакетном парсинге. Можно настроить через параметр `maxConcurrent` в `ParseProductsAsync`.

### Задержки
- Между запросами: 500 мс
- Между страницами пагинации: 1000 мс
- Между категориями: 2000 мс

## Ограничения

1. **Структура сайта**: Парсер зависит от структуры HTML сайта vseinstrumenti.ru. При изменениях в верстке可能需要 обновить CSS-селекторы.
2. **Rate limiting**: Слишком частые запросы могут привести к временной блокировке.
3. **Юридические аспекты**: Убедитесь, что использование парсера соответствует условиям использования сайта и законодательству.

## Лицензия

MIT