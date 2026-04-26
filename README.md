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
│   ├── HtmlLoader.cs           # Загрузка HTML с Polly
│   ├── RequestPolicyExecutor.cs # Политики устойчивости
│   ├── DataSanitizer.cs        # Очистка данных
│   ├── HttpClientService.cs    # HTTP-клиент (backward compat)
│   ├── CategoryParser.cs       # Парсер категорий
│   ├── ProductParser.cs        # Парсер товаров
│   ├── VoltProductParser.cs    # Парсер 220-volt.ru
│   └── VseinstrumentiParserService.cs # Основной сервис
├── Utilities/
│   └── RetryPolicy.cs          # Политика повторных попыток
├── Tests/
│   ├── UnitTests/
│   │   ├── DataSanitizerTests.cs
│   │   ├── HtmlLoaderTests.cs
│   │   ├── RequestPolicyExecutorTests.cs
│   │   ├── VoltProductParserTests.cs
│   │   ├── CategoryParserTests.cs
│   │   └── Fixtures/           # HTML-фикстуры для тестов
│   └── VseinstrumentiParser.Tests.csproj
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
using VseinstrumentiParser.Services.DependencyInjection;

// Создаём Host для DI
using var host = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddParserHttpClient(configuration);
        services.AddSingleton<IHtmlLoader, HtmlLoader>();
        services.AddScoped<ICategoryParser, CategoryParser>();
        services.AddScoped<IProductParser, ProductParser>();
        services.AddScoped<VseinstrumentiParserService>();
    })
    .Build();

await host.StartAsync();

var parserService = host.Services.GetRequiredService<VseinstrumentiParserService>();

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

## Тестирование

### Запуск всех тестов

```bash
cd Tests
dotnet test --verbosity normal
```

### Ожидаемые результаты

- `DataSanitizerTests`: 14 тестов passed ✓
- `HtmlLoaderTests`: 10 тестов passed ✓
- `RequestPolicyExecutorTests`: 11 тестов passed ✓
- `VoltProductParserTests`: 10 тестов passed ✓
- `CategoryParserTests`: 12 тестов passed ✓
- **Итого**: 57 юнит-тестов

### Генерация отчёта о покрытии

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Пример теста

```csharp
[Fact]
public async Task ParseProduct_WithValidHtml_ReturnsProduct()
{
    // Arrange
    var htmlLoaderMock = new Mock<IHtmlLoader>();
    var html = File.ReadAllText("Fixtures/product-page.html");
    htmlLoaderMock.Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(html);
    
    var parser = new VoltProductParser(htmlLoaderMock.Object);
    
    // Act
    var product = await parser.ParseProductAsync("https://example.com/product/123");
    
    // Assert
    Assert.NotNull(product);
    Assert.Equal("Дрель ударная Bosch GSB 18V", product.Name);
}
```

## Настройка

### User-Agent
По умолчанию используется:
```
Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36
```

Можно изменить в `HttpClientSettings` в конфигурации.

### Настройки Polly
```json
{
  "HttpClientSettings": {
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "InitialRetryDelayMs": 1000,
    "MaxRetryDelayMs": 10000,
    "CircuitBreakerExceptionCount": 5,
    "CircuitBreakerDurationMinutes": 5
  }
}
```

### Параллелизм
По умолчанию используется до 5 одновременных запросов при пакетном парсинге. Можно настроить через параметр `maxConcurrent` в `ParseProductsAsync`.

### Задержки
- Между страницами пагинации: 1000 мс
- Между категориями: 2000 мс

## Обработка ошибок

Сервис включает встроенную политику повторных попыток через Polly:
- **Retry**: до 3 попыток с экспоненциальной задержкой
- **Timeout**: 30 секунд на запрос
- **Circuit Breaker**: открывает цепь после 5 ошибок

Ошибки логируются и оборачиваются в `HttpRequestException`:
```
[14:30:25] Retry attempt 1 for https://example.com: Connection timeout. Waiting 1000ms
[14:30:26] Retry attempt 2 for https://example.com: Connection timeout. Waiting 2000ms
[14:30:28] Failed to load https://example.com after 3 attempts
```