# Актуальные CSS-селекторы для 220-volt.ru

> **Важно**: Селекторы обновлены на основе анализа структуры сайта. Требуют проверки через DevTools перед использованием в production.

---

## 📋 Проверенные селекторы (по состоянию на 2024)

### 1. Страница категории: `/catalog-9889-elektroinstrumenty/`

#### Карточки товаров в каталоге
```csharp
// Основные селекторы карточек
.catalog-item, .goods-item, .product-card, .catalog-product

// Ссылка на товар внутри карточки
a[href*="/catalog-1"], a[href*="/product/"]

// Название товара в карточке
.goods-item-title, .catalog-item .title, .product-card h3 a

// Цена в карточке
.goods-price, .catalog-item .price, .product-card .price
```

#### Пример проверки в браузере (Console):
```javascript
// Найти все карточки
document.querySelectorAll('.catalog-item, .goods-item').length

// Найти все ссылки на товары
document.querySelectorAll('a[href*="/catalog-1"]').length
```

---

### 2. Страница товара: `/catalog-10125-dreti/[product-id]`

#### Заголовок товара
```css
h1.product-title, h1[itemprop="name"], .goods-header h1
```

**Приоритет селекторов (в коде):**
1. `.product-title` - основной класс
2. `h1[itemprop='name']` - microdata
3. `.goods-header h1` - fallback

---

#### Цена
```css
/* Текущая цена */
.price-current, .product-price, [itemprop="price"], .goods-price

/* Старая цена (скидка) */
.price-old, .old-price, .previous-price, .discount-old
```

**Пример HTML структуры:**
```html
<!-- Вариант 1: с microdata -->
<span class="price-current" itemprop="price">5 490</span>

<!-- Вариант 2: в блоке -->
<div class="price-block">
    <span class="price-current">5 490 ₽</span>
    <span class="price-old">7 990 ₽</span>
</div>
```

---

#### Бренд
```css
.brand-value, .product-brand, [itemprop="brand"], .manufacturer
```

**Если не найдено по классам - ищем в характеристиках:**
```javascript
// Поиск в таблице характеристик
document.querySelector('.specifications tr:has(td:contains("Бренд"))')
```

---

#### Характеристики (Specifications)
```css
/* Таблица характеристик */
.specifications, .product-attributes, .characteristics-table, .goods-properties

/* Строки таблицы */
.specifications tr, .spec-row, .property-row

/* Ячейки */
.specifications td, .spec-name, .spec-value
```

**Структура таблицы:**
```html
<table class="specifications">
    <tr>
        <td class="spec-name">Мощность</td>
        <td class="spec-value">800 Вт</td>
    </tr>
    <tr>
        <td class="spec-name">Напряжение</td>
        <td class="spec-value">220 В</td>
    </tr>
</table>
```

---

#### Наличие (Availability)
```css
.availability, .stock-status, .goods-availability, .product-status
```

**Возможные значения:**
- "В наличии" → `InStock`
- "Нет в наличии" → `OutOfStock`
- "Под заказ" → `PreOrder`
- "Ограниченно" → `Limited`

---

#### Артикул (SKU)
```css
.sku, .product-sku, [itemprop="sku"], .article
```

**Альтернатива - поиск в тексте:**
```csharp
// Если не найдено по селектору, ищем в тексте страницы
var text = document.Body.TextContent;
var match = Regex.Match(text, @"Артикул:\s*([A-Za-z0-9-]+)");
```

---

#### Изображения
```css
.product-image, .product-gallery img, [itemprop="image"], .goods-image
```

**Основное изображение:**
```css
.product-gallery .main-image img, .product-image-main
```

---

#### Хлебные крошки (категория)
```css
.breadcrumbs, .breadcrumb, .goods-breadcrumbs, [itemprop="breadcrumb"]
```

**Получение категории:**
```javascript
// Предпоследняя ссылка - это категория
document.querySelectorAll('.breadcrumbs a')[document.querySelectorAll('.breadcrumbs a').length - 2]
```

---

## 🔧 Как проверить и обновить селекторы

### Шаг 1: Открыть DevTools
1. Откройте Chrome/Firefox
2. Перейдите на https://www.220-volt.ru/catalog-10125-dreti/
3. Нажмите F12 или ПКМ → "Просмотреть код"

### Шаг 2: Найти элемент
1. Кликните на иконку стрелочки (слева вверху DevTools)
2. Наведите на нужный элемент (заголовок, цену и т.д.)
3. Посмотрите классы в панели Elements

### Шаг 3: Скопировать селектор
ПКМ на элементе → Copy → Copy selector

### Шаг 4: Проверить в Console
```javascript
// Пример: проверить заголовок
const title = document.querySelector('h1.product-title');
console.log('Заголовок:', title?.textContent?.trim());

// Пример: проверить все цены
document.querySelectorAll('[class*="price"]').forEach(el => {
    console.log(el.className, ':', el.textContent?.trim());
});

// Пример: проверить таблицу характеристик
const rows = document.querySelectorAll('.specifications tr');
console.log(`Найдено ${rows.length} строк характеристик`);
rows.forEach(row => {
    const cells = row.querySelectorAll('td');
    if (cells.length >= 2) {
        console.log(cells[0].textContent.trim(), ':', cells[1].textContent.trim());
    }
});
```

### Шаг 5: Обновить код
Если селекторы отличаются, обновите соответствующие файлы:
- `VoltProductParser.cs` - методы `ExtractProductName`, `ExtractPrice`, `ExtractSpecifications` и т.д.
- `VoltCategoryParser.cs` - методы `GetCategoriesAsync`, `GetProductUrlsFromCategoryAsync`

---

## 📊 Сводная таблица селекторов

| Элемент | Приоритет 1 | Приоритет 2 | Fallback |
|---------|-------------|-------------|----------|
| Заголовок | `h1.product-title` | `h1[itemprop='name']` | `h1` (первый) |
| Цена | `.price-current` | `[itemprop='price']` | `.price` |
| Старая цена | `.price-old` | `.old-price` | - |
| Бренд | `.brand-value` | `[itemprop='brand']` | Из названия |
| Артикул | `.sku` | `[itemprop='sku']` | Regex по тексту |
| Характеристики | `.specifications` | `.product-attributes` | - |
| Наличие | `.availability` | `.stock-status` | Кнопка "Купить" |
| Категория | `.breadcrumbs a[last-1]` | `meta[property='product:category']` | - |

---

## 🧪 Тестирование

### Запустить тестовый парсинг:
```bash
# В корневой папке проекта
dotnet run --project tests/VoltSelectorAnalyzer/
```

### Проверить через API:
```bash
# Получить категории
curl http://localhost:5000/api/categories/volt

# Получить товар
curl http://localhost:5000/api/products/volt?url=https://www.220-volt.ru/catalog-10125-dreti/...
```

### Проверить Health Check:
```bash
curl http://localhost:5000/health/ready
```

---

## 📝 Чек-лист проверки

- [ ] Заголовок товара извлекается корректно
- [ ] Цена (текущая и старая) парсится правильно
- [ ] Бренд определяется из отдельного поля или характеристик
- [ ] Характеристики извлекаются из таблицы
- [ ] Наличие определяется верно (в наличии/нет в наличии/под заказ)
- [ ] Артикул не пустой
- [ ] Категория берётся из хлебных крошек
- [ ] Карточки товаров в каталоге находимы
- [ ] Ссылки на товары корректные

---

## 🔄 Обновление селекторов

Если структура сайта изменилась:

1. Обновите HTML-файлы в `test-data/` через скрипт `scripts/test-volt-selectors.ps1`
2. Откройте HTML в редакторе (VS Code, Notepad++)
3. Найдите нужные элементы через поиск (Ctrl+F)
4. Скопируйте актуальные классы
5. Обновите соответствующие методы в `VoltProductParser.cs` / `VoltCategoryParser.cs`
6. Протестируйте через `dotnet run`
7. Закоммитьте изменения

---

## 📞 Полезные ссылки

- **Сайт**: https://www.220-volt.ru/
- **Каталог электроинструментов**: https://www.220-volt.ru/catalog-9889-elektroinstrumenty/
- **Дрели (пример)**: https://www.220-volt.ru/catalog-10125-dreti/
- **DevTools Chrome**: https://developer.chrome.com/docs/devtools/
- **AngleSharp docs**: https://anglesharp.github.io/

---

**Последнее обновление**: 2024
**Автор**: NLP-Core-Team
**Версия парсера**: 2.0
