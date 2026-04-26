# Следующие шаги для парсеров 220-volt.ru

## 🔹 1. Адаптация селекторов под реальный сайт

Сейчас в тесте используется заглушка `https://example.com/product`. Для продакшена необходимо:

### Как найти правильные селекторы:
1. **Откройте DevTools на 220-volt.ru** (F12)
2. **Перейдите на страницу категории**, например:  
   `https://www.220-volt.ru/catalog-9889-elektroinstrumenty/dreli-udarnye/`
3. **Найдите нужный элемент** (например, название товара)
4. **Скопируйте CSS-селектор**:
   - Правый клик → Copy → Copy selector
   - Или используйте уникальные атрибуты: `class`, `data-*`, `id`

### Примеры обновления селекторов в коде:

**В `VoltCategoryParser.cs`:**
```csharp
// Заменить заглушки на реальные селекторы
var categoryElements = document.QuerySelectorAll(".catalog-section-item, .category-card");
var productLinks = document.QuerySelectorAll(".product-card a, .goods-item-title a");
```

**В `VoltProductParser.cs`:**
```csharp
// Название товара
var nameElement = document.QuerySelector(".product-title, h1[itemprop='name']");

// Цена
var priceElement = document.QuerySelector(".price-block, [itemprop='price']");

// Характеристики
var specTable = document.QuerySelector(".specifications, .product-attributes");
```

### Инструменты для тестирования селекторов:
```csharp
// Временный метод для отладки
private void DebugSelectors(IDocument document)
{
    var test = document.QuerySelector(".product-title");
    _logger.Log($"Найден элемент: {test?.OuterHtml?.Substring(0, 100)}");
}
```

## 🔹 2. Запуск параллельного парсинга двух сайтов

### Конфигурация в `appsettings.json`:
```json
{
  "ParserSettings": {
    "Sources": [
      {
        "Name": "Vseinstrumenti",
        "BaseUrl": "https://www.vseinstrumenti.ru",
        "CategoryPath": "/category/elektroinstrumenty/",
        "ParserType": "Vseinstrumenti"
      },
      {
        "Name": "220Volt",
        "BaseUrl": "https://www.220-volt.ru",
        "CategoryPath": "/catalog-9889-elektroinstrumenty/",
        "ParserType": "220Volt"
      }
    ],
    "Parallelism": {
      "MaxConcurrentParsers": 2,
      "DelayBetweenRequestsMs": 1000
    }
  }
}
```

### Запуск параллельного парсинга:
```bash
# Через основной проект
dotnet run --project VseinstrumentiParser.csproj --parallel

# Или через скрипт
scripts/run-parallel-parsers.ps1
```

### Пример скрипта `run-parallel-parsers.ps1`:
```powershell
$jobs = @(
    { dotnet run --project VseinstrumentiParser.csproj --source vseinstrumenti },
    { dotnet run --project VseinstrumentiParser.csproj --source 220volt }
)

$jobs | ForEach-Object {
    Start-Job -ScriptBlock $_
}

Get-Job | Wait-Job | Receive-Job
```

## 🔹 3. Добавление 220-volt.ru в дашборд Grafana

### Обновление `grafana/dashboards/parser-metrics.json`:
```json
{
  "panels": [
    {
      "title": "220-volt.ru - Статистика парсинга",
      "targets": [
        {
          "expr": "rate(parser_products_parsed_total{source=\"220volt\"}[5m])",
          "legendFormat": "{{category}} - товаров/мин"
        }
      ],
      "type": "graph",
      "gridPos": { "h": 8, "w": 12, "x": 0, "y": 0 }
    },
    {
      "title": "Сравнение источников",
      "targets": [
        {
          "expr": "parser_products_parsed_total{source=\"vseinstrumenti\"}",
          "legendFormat": "Vseinstrumenti"
        },
        {
          "expr": "parser_products_parsed_total{source=\"220volt\"}",
          "legendFormat": "220-volt.ru"
        }
      ],
      "type": "stat",
      "gridPos": { "h": 8, "w": 12, "x": 12, "y": 0 }
    }
  ]
}
```

### Метрики для Prometheus (добавить в код):
```csharp
// В VoltParserService.cs
private readonly Counter _productsParsedCounter = Metrics
    .CreateCounter("parser_products_parsed_total", "Number of parsed products", 
        new CounterConfiguration { LabelNames = new[] { "source", "category" } });

// При парсинге товара
_productsParsedCounter.WithLabels("220volt", category.Name).Inc();
```

## 🔹 4. Настройка отдельных алертов для нового источника

### Обновление `alerts/basic-availability-alert.yaml`:
```yaml
groups:
  - name: parser_alerts
    rules:
      - alert: Parser220VoltDown
        expr: up{job="parser", source="220volt"} == 0
        for: 5m
        labels:
          severity: critical
          source: 220volt
        annotations:
          summary: "Парсер 220-volt.ru недоступен"
          description: "Парсер для 220-volt.ru не отвечает более 5 минут"
      
      - alert: Parser220VoltHighErrorRate
        expr: rate(parser_errors_total{source="220volt"}[5m]) > 0.1
        for: 2m
        labels:
          severity: warning
          source: 220volt
        annotations:
          summary: "Высокий уровень ошибок парсера 220-volt.ru"
          description: "Более 10% запросов к 220-volt.ru завершаются ошибкой"
      
      - alert: Parser220VoltNoNewProducts
        expr: increase(parser_products_parsed_total{source="220volt"}[1h]) == 0
        for: 1h
        labels:
          severity: warning
          source: 220volt
        annotations:
          summary: "Парсер 220-volt.ru не находит новые товары"
          description: "За последний час не было распарсено ни одного товара"
```

## 🔹 5. ParserFactory для масштабирования

### Реализация фабрики парсеров:

**`Services/ParserFactory.cs`:**
```csharp
public interface IParserFactory
{
    ICategoryParser CreateCategoryParser(string source);
    IProductParser CreateProductParser(string source);
}

public class ParserFactory : IParserFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ParserFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public ICategoryParser CreateCategoryParser(string source)
    {
        return source.ToLower() switch
        {
            "vseinstrumenti" => _serviceProvider.GetRequiredService<VseinstrumentiCategoryParser>(),
            "220volt" => _serviceProvider.GetRequiredService<VoltCategoryParser>(),
            _ => throw new NotSupportedException($"Источник {source} не поддерживается")
        };
    }

    public IProductParser CreateProductParser(string source)
    {
        return source.ToLower() switch
        {
            "vseinstrumenti" => _serviceProvider.GetRequiredService<VseinstrumentiProductParser>(),
            "220volt" => _serviceProvider.GetRequiredService<VoltProductParser>(),
            _ => throw new NotSupportedException($"Источник {source} не поддерживается")
        };
    }
}
```

### Регистрация в DI (`Services/DependencyInjection/ServiceCollection.cs`):
```csharp
public static IServiceCollection AddParsers(this IServiceCollection services)
{
    services.AddTransient<VseinstrumentiCategoryParser>();
    services.AddTransient<VseinstrumentiProductParser>();
    services.AddTransient<VoltCategoryParser>();
    services.AddTransient<VoltProductParser>();
    services.AddSingleton<IParserFactory, ParserFactory>();
    
    return services;
}
```

### Использование фабрики:
```csharp
public class MultiSourceParserService
{
    private readonly IParserFactory _parserFactory;
    
    public async Task ParseAllSources()
    {
        var sources = new[] { "vseinstrumenti", "220volt" };
        
        foreach (var source in sources)
        {
            var categoryParser = _parserFactory.CreateCategoryParser(source);
            var productParser = _parserFactory.CreateProductParser(source);
            
            // Парсинг категорий и товаров
            var categories = await categoryParser.GetCategoriesAsync();
            // ...
        }
    }
}
```

### Добавление нового сайта (3 шага):
1. **Создать классы парсеров**:
   ```csharp
   public class NewSiteCategoryParser : ICategoryParser { ... }
   public class NewSiteProductParser : IProductParser { ... }
   ```

2. **Зарегистрировать в DI**:
   ```csharp
   services.AddTransient<NewSiteCategoryParser>();
   services.AddTransient<NewSiteProductParser>();
   ```

3. **Добавить в фабрику**:
   ```csharp
   case "newsite" => _serviceProvider.GetRequiredService<NewSiteCategoryParser>(),
   ```

4. **Настроить в конфигурации**:
   ```json
   {
     "Name": "NewSite",
     "BaseUrl": "https://newsite.ru",
     "ParserType": "newsite"
   }
   ```

## 🔹 6. Мониторинг и оптимизация

### Ключевые метрики для отслеживания:
- `parser_duration_seconds` - время парсинга
- `parser_products_parsed_total` - количество распарсенных товаров
- `parser_errors_total` - количество ошибок
- `parser_http_requests_total` - количество HTTP-запросов

### Оптимизации производительности:
1. **Кэширование** - использовать `CacheService` для хранения HTML страниц
2. **Распределенная очередь** - RabbitMQ/Kafka для задач парсинга
3. **Балансировка нагрузки** - несколько инстансов парсеров
4. **Геораспределение** - прокси в разных регионах

## 🔹 7. Деплой и CI/CD

### Пример `.gitlab-ci.yml` для автоматического тестирования:
```yaml
test_220volt_parser:
  stage: test
  script:
    - dotnet test --filter "Category=Integration&Source=220volt"
    - dotnet run --project TestVoltParsers/TestVoltParsers.csproj -- --validate-selectors
```

### Health checks для мониторинга:
```csharp
// В ProgramWithHealthChecks.cs
services.AddHealthChecks()
    .AddCheck<ExternalSitesHealthCheck>("220volt.ru", 
        tags: new[] { "external", "parser" })
    .AddUrlGroup(new Uri("https://www.220-volt.ru"), "220volt-site");
```

## Заключение

Парсеры для 220-volt.ru готовы к интеграции в продакшен. Приоритетные шаги:

1. **Адаптировать селекторы** под реальную структуру сайта
2. **Настроить мониторинг** в Grafana/Prometheus
3. **Реализовать ParserFactory** для удобного масштабирования
4. **Добавить алерты** для быстрого реагирования на проблемы

После выполнения этих шагов система будет готова к парсингу двух источников параллельно с полным мониторингом и отказоустойчивостью.