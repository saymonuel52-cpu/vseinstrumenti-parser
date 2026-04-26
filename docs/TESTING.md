# Тестирование парсера vseinstrumenti

## 📊 Обзор тестового покрытия

### Единицы тестирования (Unit Tests)

| Компонент | Тестов | Покрытие | Описание |
|-----------|--------|----------|----------|
| `DataSanitizer` | 14 | 100% | Очистка текста, цен, брендов, характеристик |
| `HtmlLoader` | 10 | 90% | Загрузка HTML с политиками устойчивости |
| `RequestPolicyExecutor` | 11 | 85% | Retry, timeout, circuit breaker |
| `VoltProductParser` | 10 | 70% | Парсинг товаров 220-volt.ru |
| `CategoryParser` | 12 | 65% | Парсинг категорий и товаров |
| **Итого** | **57** | **~80%** | |

---

## 🧪 Структура тестов

```
Tests/
├── UnitTests/
│   ├── DataSanitizerTests.cs          # 14 тестов
│   ├── HtmlLoaderTests.cs             # 10 тестов
│   ├── RequestPolicyExecutorTests.cs  # 11 тестов
│   ├── VoltProductParserTests.cs      # 10 тестов
│   ├── CategoryParserTests.cs         # 12 тестов
│   └── Fixtures/
│       ├── product-page.html          # Фикстура товара
│       ├── category-page.html         # Фикстура категории
│       ├── main-page.html             # Главная страница
│       ├── category-drili-page.html   # Дрели (4 товара)
│       ├── empty-category.html        # Пустая категория
│       ├── error-page.html            # 404 страница
│       └── 503-page.html              # 503 Service Unavailable
└── VseinstrumentiParser.Tests.csproj
```

---

## 🏃 Запуск тестов

### Базовый запуск

```powershell
cd Tests
dotnet test
```

### С детальным выводом

```powershell
dotnet test --verbosity normal --logger "console;verbosity=detailed"
```

### Только конкретный класс

```powershell
dotnet test --filter "FullyQualifiedName~DataSanitizerTests"
```

### С покрытием кода

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

Отчёт будет в: `Tests/TestResults/<guid>/coverage.cobertura.xml`

---

## 📝 Паттерн тестирования

Все тесты следуют паттерну **Arrange-Act-Assert**:

### Пример 1: Успешный парсинг

```csharp
[Fact]
public async Task ParseProduct_ValidHtml_ReturnsProduct()
{
    // Arrange
    var htmlLoaderMock = new Mock<IHtmlLoader>();
    var html = File.ReadAllText("Fixtures/product-page.html");
    htmlLoaderMock
        .Setup(x => x.LoadHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(html);
    
    var parser = new VoltProductParser(htmlLoaderMock.Object);
    
    // Act
    var product = await parser.ParseProductAsync("https://example.com/product/123");
    
    // Assert
    Assert.NotNull(product);
    Assert.Equal("Дрель ударная Bosch", product.Name);
    Assert.Equal(8490m, product.Price);
}
```

### Пример 2: Обработка ошибок

```csharp
[Fact]
public async Task LoadHtmlAsync_RequestFails_ThrowsException()
{
    // Arrange
    var url = "https://example.com/404";
    _policyExecutorMock
        .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new RequestResult
        {
            Success = false,
            Exception = new HttpRequestException("Not found")
        });
    
    var htmlLoader = new HtmlLoader(_policyExecutorMock.Object, _settings, _logger);
    
    // Act & Assert
    await Assert.ThrowsAsync<HttpRequestException>(() => htmlLoader.LoadHtmlAsync(url));
}
```

### Пример 3: Проверка политик

```csharp
[Fact]
public async Task ExecuteGetAsync_ServerError_RetryAttemptsMade()
{
    // Arrange
    var callCount = 0;
    _httpMessageHandlerMock
        .SetupHandler(HttpMethod.Get, url)
        .Returns(() =>
        {
            callCount++;
            return callCount == 1 
                ? HttpStatusCode.ServiceUnavailable 
                : HttpStatusCode.OK;
        });
    
    // Act
    var result = await _executor.ExecuteGetAsync(url);
    
    // Assert
    Assert.True(result.Success);
    Assert.Equal(2, callCount); // Один ретрай
}
```

---

## 📁 HTML-фикстуры

### product-page.html
Страница товара на 220-volt.ru:
- Название: `<h1 class="product-title">`
- Цена: `<span class="price-current">`
- Бренд: `<span class="brand">`
- Характеристики: `<table class="specifications">`

### category-page.html
Страница категории с товарами:
- Карточки товаров: `<article class="product-card">`
- Ссылки: `<a href="/product/...">`
- Пагинация: `<nav class="pagination">`

### error-page.html
Страница 404:
- `<h1>404 - Страница не найдена</h1>`

### empty-category.html
Пустая категория:
- `<div class="empty-category">`

### 503-page.html
Ошибка сервера:
- `<h1>503 - Service Unavailable</h1>`

---

## 🎯 Покрытие сценариев

### DataSanitizer

| Метод | Тесты | Сценарии |
|-------|-------|----------|
| `CleanText()` | 3 | Множественные пробелы, переносы, null |
| `CleanProductName()` | 2 | Префикс "Купить", пустое значение |
| `TryParsePrice()` | 3 | ₽, запятая, некорректные значения |
| `CleanBrand()` | 1 | Нормализация регистра |
| `CleanArticle()` | 1 | Удаление пробелов |
| `NormalizeSpecificationKey()` | 2 | Синонимы, voltage |
| `StripHtmlTags()` | 1 | HTML-теги |
| `CleanUrl()` | 2 | Query параметры, null |

### HtmlLoader

| Сценарий | Тест | Результат |
|----------|------|-----------|
| Успешный запрос | `LoadHtmlAsync_SuccessfulRequest` | Возвращает HTML |
| Ошибка запроса | `LoadHtmlAsync_RequestFails` | Throws HttpRequestException |
| Circuit breaker открыт | `LoadHtmlAsync_CircuitBreakerOpen` | Throws CircuitBreakerOpenException |
| Кастомные заголовки | `LoadHtmlAsync_WithCustomHeaders` | Применяет заголовки |
| Пустой контент | `LoadHtmlAsync_EmptyContent` | Возвращает пустую строку |
| Большой контент | `LoadHtmlAsync_LargeContent` | 1MB без проблем |
| Отмена | `LoadHtmlAsync_CancellationRequested` | OperationCanceledException |
| Таймаут | `LoadHtmlAsync_TimeoutException` | TimeoutRejectedException |

### RequestPolicyExecutor

| Сценарий | Тест | Результат |
|----------|------|-----------|
| Успешный ответ | `ExecuteGetAsync_SuccessfulResponse` | Success = true |
| Серверная ошибка | `ExecuteGetAsync_ServerError` | Retry attempts made |
| 404 Not Found | `ExecuteGetAsync_NotFound` | Success = false |
| Успех после ретрая | `ExecuteGetAsync_SuccessfulAfterRetry` | Success = true |
| Кастомные заголовки | `ExecuteGetAsync_WithCustomHeaders` | Headers included |
| Таймаут | `ExecuteGetAsync_Timeout` | TimeoutRejectedException |
| Отмена | `ExecuteGetAsync_CancellationRequested` | OperationCanceledException |
| Проверка URL | `CheckUrlAsync_ValidUrl` | Returns true/false |

### VoltProductParser

| Сценарий | Тест | Результат |
|----------|------|-----------|
| Название товара | `ParseProductAsync_ValidHtml_ReturnsProductWithName` | Name extracted |
| Цена | `ParseProductAsync_ValidHtml_ReturnsCorrectPrice` | 8490m |
| Бренд | `ParseProductAsync_ValidHtml_ReturnsCorrectBrand` | "Bosch" |
| Артикул | `ParseProductAsync_ValidHtml_ReturnsCorrectArticle` | "06019H6100" |
| Наличие | `ParseProductAsync_ValidHtml_ReturnsInStockAvailability` | InStock |
| Характеристики | `ParseProductAsync_ValidHtml_ReturnsSpecifications` | Dict not empty |
| Пустой HTML | `ParseProductAsync_EmptyHtml` | Default values |
| Ошибка загрузки | `ParseProductAsync_HtmlLoaderThrows` | Throws exception |
| Частичная цена | `ParseProductAsync_PartialPrice` | 15200m |
| Отмена | `ParseProductAsync_CancellationRequested` | OperationCanceledException |

### CategoryParser

| Сценарий | Тест | Результат |
|----------|------|-----------|
| Категории | `GetCategoriesAsync_ValidHtml` | Not empty |
| Пустая HTML | `GetCategoriesAsync_EmptyHtml` | Empty list |
| Товары | `GetProductUrlsFromCategoryAsync_ValidHtml` | URLs extracted |
| Пустая категория | `GetProductUrlsFromCategoryAsync_EmptyCategory` | Empty list |
| Подкатегории | `GetSubCategoriesAsync_ValidHtml` | Not null |
| Ошибка загрузки | `GetCategoriesAsync_HtmlLoaderThrows` | Throws exception |
| Несколько страниц | `GetProductUrlsFromCategoryAsync_MultiplePages` | All pages parsed |
| Остановка при пустой | `GetProductUrlsFromCategoryAsync_WithPagination` | Stops early |
| Отмена | `GetCategoriesAsync_CancellationRequested` | OperationCanceledException |
| Лимит страниц | `GetProductUrlsFromCategoryAsync_TooManyPages` | maxPages respected |

---

## 🔍 Отладка тестов

### Запуск конкретного теста

```powershell
dotnet test --filter "Name=ParseProduct_WithValidHtml_ReturnsProduct"
```

### С выводом логов

```powershell
dotnet test --logger "console;verbosity=detailed" --filter "Name=DataSanitizerTests"
```

### С покрытием

```powershell
dotnet test --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"
```

### В Visual Studio

1. Откройте Test Explorer (Ctrl+E, T)
2. Найдите тест
3. Нажмите Run или Debug
4. Используйте Breakpoints для отладки

---

## 📈 Метрики качества

### Целевые показатели

| Метрика | Цель | Текущее |
|---------|------|---------|
| Покрытие кода | >70% | ~80% ✓ |
| Количество тестов | >50 | 57 ✓ |
| Время выполнения | <30 сек | ~15 сек ✓ |
| Пропуски | 0 | 0 ✓ |
| Ошибки | 0 | 0 ✓ |

---

## 🚀 CI/CD интеграция

### GitHub Actions

```yaml
name: Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v2
        with:
          dotnet-version: 8.0
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Run tests
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
      - name: Upload coverage
        uses: codecov/codecov-action@v2
```

---

## 🤝 Добавление новых тестов

### Шаги:

1. Создайте фикстуру в `Tests/UnitTests/Fixtures/`
2. Напишите тестовый метод с атрибутом `[Fact]`
3. Используйте паттерн Arrange-Act-Assert
4. Мокируйте внешние зависимости (IHtmlLoader, HttpClient)
5. Запустите тесты: `dotnet test`
6. Проверьте покрытие

### Пример:

```csharp
[Fact]
public async Task NewMethod_WithValidInput_ReturnsExpectedResult()
{
    // Arrange
    var mock = new Mock<IDependency>();
    var sut = new SystemUnderTest(mock.Object);
    
    // Act
    var result = await sut.NewMethodAsync("input");
    
    // Assert
    Assert.Equal("expected", result);
}
```

---

**Последнее обновление**: 2024-01-15  
**Версия тестов**: 2.0  
**Покрытие**: ~80%
