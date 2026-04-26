# Архитектура парсера vseinstrumenti

## 🎯 Принципы проектирования

### 1. Надёжность прежде новых фич
- Все HTTP-запросы через IHttpClientFactory + Polly
- Автоматические retry, timeout, circuit breaker
- Явная обработка ошибок, никаких "глотанных" исключений

### 2. Композиция вместо наследования
- Каждый парсер использует готовые сервисы через DI
- Нет жёсткой иерархии классов
- Легко тестировать компоненты изолированно

### 3. Прозрачность и предсказуемость
- Явные типы, понятные имена методов
- Логирование только в точках принятия решений
- Никакой магии, всё видно в коде

---

## 🏗️ Архитектурные решения

### IHttpClientFactory + Polly

**Почему:**
- Устраняет проблему Socket Exhaustion (утечки сокетов)
- Централизованная политика устойчивости
- Простое тестирование через моки
- Переиспользование HttpClient между запросами

**Риски устранённые:**
```
❌ HttpClient() в каждом методе → истощение сокетов
❌ Нет retry при временных сбоях → потеря данных
❌ Нет timeout → зависание на неопределённое время
❌ Нет circuit breaker → каскадные сбои
```

**Как реализовано:**
```csharp
services.AddHttpClient("ParserClient")
    .AddPolicyHandler(Policy.TimeoutAsync(TimeSpan.FromSeconds(30)))
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

### Композиция парсеров

**Почему:**
- Каждый компонент отвечает за ОДНУ задачу (SRP)
- Изменяешь HtmlLoader → не ломаются парсеры
- Тестируешь каждый компонент отдельно
- Новые парсеры пишутся за 10 минут

**Структура:**
```
┌─────────────────────────────────────────┐
│  VseinstrumentiParserService            │
│  (координирует процесс парсинга)        │
└─────────────┬───────────────────────────┘
              │
    ┌─────────┴─────────┐
    │                   │
┌───▼──────┐    ┌──────▼──────┐
│ IHtmlLoader  │  ICategoryParser  │
│ (загрузка)   │  (категории)      │
└────────────┘    └─────────────────┘
    ▲
    │ использует
    │
┌───┴───────────────────────────────────┐
│  HtmlLoader                           │
│  - RequestPolicyExecutor              │
│  - логирование                        │
└───────────────────────────────────────┘
              ▲
              │ использует
              │
┌─────────────┴───────────────┐
│  RequestPolicyExecutor      │
│  - IHttpClientFactory       │
│  - Polly policies           │
│  - метрики                  │
└─────────────────────────────┘

┌─────────────────────────────────────────┐
│  ProductParser / CategoryParser         │
│  - IHtmlLoader (загрузка HTML)          │
│  - DataSanitizer (очистка данных)       │
│  - IBrowsingContext (парсинг HTML)      │
└─────────────────────────────────────────┘
              ▲
              │ использует
              │
┌─────────────┴───────────────┐
│  DataSanitizer              │
│  - CleanText                │
│  - TryParsePrice            │
│  - CleanBrand               │
│  - NormalizeSpecificationKey│
└─────────────────────────────┘
```

**Компоненты:**

| Компонент | Ответственность | Зависимости |
|-----------|----------------|-------------|
| `IHtmlLoader` | Загрузка HTML по URL | RequestPolicyExecutor |
| `RequestPolicyExecutor` | Retry, timeout, circuit breaker | IHttpClientFactory, Polly |
| `DataSanitizer` | Очистка и нормализация данных | Regex |
| `ProductParser` | Парсинг товара | IHtmlLoader, DataSanitizer |
| `CategoryParser` | Парсинг категорий | IHtmlLoader, DataSanitizer |

### Обработка ошибок

**Политика:**
1. Логировать с контекстом (URL, попытка, задержка)
2. Пробрасывать дальше, если можно обработать
3. Сворачивать в семантические исключения

**Пример:**
```csharp
try
{
    var html = await _htmlLoader.LoadHtmlAsync(url);
}
catch (CircuitBreakerOpenException)
{
    _logger.LogError("Circuit breaker opened for {Url}", url);
    throw; // Пробрасываем дальше
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to load {Url} after retries", url);
    throw new HttpRequestException($"Failed after 3 attempts", ex);
}
```

---

## 🧪 Тестирование

### Стратегия

**Unit-тесты (xUnit + Moq):**
- Тестируют логику парсеров
- Используют фикстуры (сохранённые HTML)
- Работают оффлайн, 100% детерминировано

**Интеграционные тесты:**
- Проверяют работу с реальным сайтом
- Запускаются только в CI/CD
- Не блокируют разработку

### Структура тестов

```
Tests/
├── UnitTests/
│   ├── VoltProductParserTests.cs
│   ├── CategoryParserTests.cs
│   ├── DataSanitizerTests.cs
│   ├── RequestPolicyExecutorTests.cs
│   └── Fixtures/
│       ├── product-page.html
│       ├── category-page.html
│       └── empty-page.html
└── IntegrationTests/
    └── E2ETests.cs
```

### Пример теста (Arrange-Act-Assert)

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
    Assert.Equal(8490m, product.Price);
    Assert.Equal("Bosch", product.Brand);
}
```

---

## 🔧 Настройка и запуск

### Регистрация в DI

```csharp
// Program.cs
services.AddParserHttpClient(configuration); // IHttpClientFactory + Polly
services.AddParserHealthChecks(configuration); // Health checks
services.AddSingleton<IHtmlLoader, HtmlLoader>();
services.AddScoped<ICategoryParser, CategoryParser>();
services.AddScoped<IProductParser, ProductParser>();
services.AddScoped<VseinstrumentiParserService>();
```

### Конфигурация (appsettings.json)

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

### Health Checks

**Эндпоинты:**
- `/health/live` - работает ли приложение (готовность к приему запросов)
- `/health/ready` - готово ли к работе (все зависимости доступны)

**Проверки:**
- Подключение к Redis
- Доступность целевых доменов (vseinstrumenti.ru, 220-volt.ru)

---

## ✅ Чек-лист верификации

### После внедрения IHttpClientFactory

- [ ] Нет явного `new HttpClient()` в коде
- [ ] Все HTTP-запросы через `IHttpClientFactory`
- [ ] Polly политики настроены (timeout, retry, circuit breaker)
- [ ] Тесты не зависят от сети (используют моки)
- [ ] Нет утечек сокетов при нагрузочном тестировании

### После рефакторинга парсеров

- [ ] Парсеры используют IHtmlLoader через DI
- [ ] Нет дублирования логики загрузки HTML
- [ ] Каждый парсер можно протестировать отдельно
- [ ] Добавлены юнит-тесты с фикстурами
- [ ] Код покрыт на 70%+

### После настройки мониторинга

- [ ] `/health/live` возвращает 200 OK
- [ ] `/health/ready` возвращает 200 OK при работе всех зависимостей
- [ ] Логи структурированы (JSON в Seq)
- [ ] Метрики экспортируются в Prometheus

---

## 🚧 Известные ограничения

### Что сознательно отложили

1. **Параллелизм** - пока используем последовательный парсинг
   - *Причина*: сложность отладки, риск блокировок со стороны сайта
   - *Когда вернёмся*: после стабилизации базового функционала

2. **Динамические сайты (JavaScript)** - не поддерживаем пока
   - *Причина*: требует Selenium/Playwright, медленнее в 10x
   - *Когда вернёмся*: если сайт перейдёт на SPA

3. **AI-анализ контента** - не используем
   - *Причина*: избыточно, CSS-селекторы справляются
   - *Когда вернёмся*: если структура сайта станет слишком хаотичной

4. **Распределённый парсинг** - пока монолит
   - *Причина*: нет необходимости для текущего масштаба
   - *Когда вернёмся*: если нужно парсить 10k+ товаров/день

### Метрики для мониторинга

Перед переходом к следующей итерации отслеживать:

| Метрика | Целевое значение | Тренд |
|---------|-----------------|-------|
| Успешность запросов | >95% | ↗️ |
| Среднее время парсинга товара | <2 сек | ↘️ |
| Количество ошибок в час | <10 | ↘️ |
| Circuit breaker срабатываний | 0 | → |
| Использование памяти | <500 MB | → |

---

## 📚 Следующие шаги

После стабилизации базовой архитектуры:

1. **Добавить параллелизм** (SemaphoreSlim с ограничением)
2. **Интегрировать Redis** для кэширования HTML
3. **Добавить экспорт в Excel/JSON**
4. **Настроить алерты** (Telegram/Slack при ошибках)
5. **Добавить UI** для управления задачами (Blazor уже готов!)

---

## 🛠️ Отладка типичных сценариев

### Парсинг не возвращает данные

1. Проверь HTML-фикстуру: `File.ReadAllText("Fixtures/product-page.html")`
2. Открой DevTools → Network → проверь реальный ответ
3. Проверь селекторы в браузере: `document.querySelector("h1.product-title")`
4. Добавь логирование: `_logger.LogDebug("HTML: {Html}", html.Substring(0, 500))`

### Circuit breaker срабатывает слишком часто

1. Увеличь `CircuitBreakerExceptionCount` в конфиге
2. Проверь, не блокирует ли сайт бота (captcha, 403)
3. Увеличь задержку между запросами (`RequestDelayMs`)

### Ошибки таймаута

1. Увеличь `TimeoutSeconds` (по умолчанию 30)
2. Проверь скорость интернета
3. Проверь, не перегружен ли сайт

---

**Версия архитектуры**: 2.0  
**Дата обновления**: 2024-01-15  
**Автор**: NLP-Core-Team
