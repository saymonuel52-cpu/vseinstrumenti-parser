# Инструкции по запуску парсеров для 220-volt.ru

## Предварительные требования

1. **Установите .NET 8.0 или выше**:
   - Скачайте с официального сайта: https://dotnet.microsoft.com/download
   - Выберите версию для вашей операционной системы
   - Установите, следуя инструкциям установщика

2. **Проверьте установку**:
   ```bash
   dotnet --version
   ```
   Должна отобразиться версия .NET (например, 8.0.100)

## Структура проекта

Проект содержит следующие ключевые файлы:

### Основные парсеры для 220-volt.ru:
- `Services/VoltCategoryParser.cs` - парсер категорий
- `Services/VoltProductParser.cs` - парсер товаров  
- `Services/VoltParserService.cs` - основной сервис

### Общие компоненты (совместимость):
- `Interfaces/ICategoryParser.cs` - интерфейс парсера категорий
- `Interfaces/IProductParser.cs` - интерфейс парсера товаров
- `Services/HttpClientService.cs` - HTTP-клиент с поддержкой Cookie
- `Utilities/RetryPolicy.cs` - политика повторных попыток
- `Models/Category.cs` и `Models/Product.cs` - модели данных

## Запуск проекта

### Вариант 1: Запуск основного примера
```bash
# Восстановите зависимости
dotnet restore

# Соберите проект
dotnet build

# Запустите программу
dotnet run
```

### Вариант 2: Запуск тестового примера для 220-volt.ru
Создайте новый файл программы или измените `Program.cs`:

```csharp
using VseinstrumentiParser.Services;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Тестирование парсеров 220-volt.ru");
        
        using var parser = new VoltParserService();
        
        // Получение категорий
        var categories = await parser.GetCategoriesAsync();
        Console.WriteLine($"Найдено категорий: {categories.Count}");
        
        // Парсинг товаров из первой категории (если есть)
        if (categories.Count > 0)
        {
            var products = await parser.GetProductsFromCategoryAsync(
                categories[0].Url, maxPages: 1);
            Console.WriteLine($"Найдено товаров: {products.Count}");
            
            // Экспорт в CSV
            await parser.ExportToCsvAsync(products, "220volt_products.csv");
        }
    }
}
```

### Вариант 3: Использование отдельных компонентов
```csharp
using VseinstrumentiParser.Services;

// Создание зависимостей
var httpClient = new HttpClientService();
var categoryParser = new VoltCategoryParser(httpClient);
var productParser = new VoltProductParser(httpClient);

// Использование парсеров
var categories = await categoryParser.GetCategoriesAsync();
var productUrls = await categoryParser.GetProductUrlsFromCategoryAsync(
    categories[0].Url, maxPages: 2);
var products = await productParser.ParseProductsAsync(productUrls);

// Очистка ресурсов
httpClient.Dispose();
```

## Настройка селекторов

Если парсер не находит данные,可能需要 обновить CSS-селекторы:

1. **Откройте сайт 220-volt.ru в браузере**
2. **Используйте инструменты разработчика (F12)**
3. **Изучите структуру HTML**:
   - Категории: `.catalog-section`, `.category-item`, `.left-menu a`
   - Товары: `.product-card`, `.goods-item`, `.catalog-item`
   - Название: `.product-title`, `h1.product-name`, `.goods-header h1`
   - Цена: `.price`, `.goods-price`, `.product-price`
   - Характеристики: `.specifications-table`, `.goods-properties`

4. **Обновите селекторы в файлах**:
   - `VoltCategoryParser.cs` - методы `GetCategoriesAsync`, `GetProductUrlsFromCategoryAsync`
   - `VoltProductParser.cs` - методы `ExtractProductName`, `ExtractPrice`, и т.д.

## Обработка динамической подгрузки

Для сайтов с динамической подгрузкой товаров:

1. **Увеличьте задержки**:
   ```csharp
   var httpClient = new HttpClientService();
   // Задержка между запросами в самом парсере уже настроена
   ```

2. **Используйте больше попыток**:
   ```csharp
   var retryPolicy = new RetryPolicy(
       maxRetries: 5,
       initialDelayMs: 3000);
   ```

3. **Эмулируйте поведение браузера**:
   ```csharp
   var httpClient = new HttpClientService(
       userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ...");
   ```

## Возможные ошибки и решения

### Ошибка: "dotnet не является командой"
- Установите .NET SDK
- Добавьте путь к dotnet в переменную PATH
- Перезапустите терминал

### Ошибка компиляции: "AngleSharp не найден"
```bash
dotnet add package AngleSharp
dotnet restore
```

### Ошибка парсинга: "Не найдены элементы"
- Обновите CSS-селекторы
- Проверьте доступность сайта
- Увеличьте задержки между запросами

### Ошибка сети: "Таймаут соединения"
- Увеличьте Timeout в HttpClientService
- Используйте RetryPolicy с большим количеством попыток
- Проверьте интернет-соединение

## Экспорт данных

Парсер поддерживает экспорт в CSV:

```csharp
await parser.ExportToCsvAsync(products, "220volt_export.csv");
```

Формат CSV включает:
- Название, цена, бренд, артикул
- Наличие, мощность, тип двигателя, напряжение
- Рейтинг, отзывы, URL, категория

## Дополнительные примеры

Смотрите также:
- `TestVoltParser.cs` - тестовый пример
- `ExampleVoltUsage.cs` - примеры использования
- `Program.cs` - демонстрационная программа

## Лицензия и использование

Убедитесь, что использование парсера соответствует:
- Условиям использования сайта 220-volt.ru
- Законодательству вашей страны
- Правилам robots.txt сайта

Рекомендуется:
- Использовать разумные задержки между запросами
- Не перегружать сервер сайта
- Использовать данные в личных или исследовательских целях