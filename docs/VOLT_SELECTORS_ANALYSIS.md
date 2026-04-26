# 220-volt.ru Selector Analysis Guide

## Как проверить актуальные CSS-селекторы

### Шаг 1: Открыть DevTools
1. Откройте браузер (Chrome/Firefox)
2. Перейдите на https://www.220-volt.ru/
3. Нажмите F12 или ПКМ → "Просмотреть код"/"Исследовать элемент"

### Шаг 2: Проанализировать страницу категории
URL: `https://www.220-volt.ru/catalog-9889-elektroinstrumenty/`

**Что искать:**
- Контейнер списка категорий/товаров
- Карточка товара
- Ссылка на товар
- Цена
- Название товара

**Как найти:**
1. Кликните на элемент (иконка стрелочки в левом верхнем углу DevTools)
2. Наведите на карточку товара
3. Посмотрите классы в панели Elements
4. Правой кнопкой на элементе → Copy → Copy selector

### Шаг 3: Проанализировать страницу товара
URL: `https://www.220-volt.ru/catalog-10125-dreti/` (или любой другой товар)

**Что искать:**
```
Заголовок: h1 или элемент с классом .product-title
Цена: элемент с классом .price или [itemprop="price"]
Старая цена: .old-price или .discount
Бренд: .brand или [itemprop="brand"]
Артикул: .sku или "Артикул:" в характеристиках
Характеристики: таблица с классом .specifications или .product-attributes
Наличие: .availability или .stock
Изображения: .product-gallery img или [itemprop="image"]
```

### Шаг 4: Проверить селекторы в Console
```javascript
// Проверить заголовок
document.querySelector('h1.product-title')?.textContent

// Проверить цену
document.querySelector('.price-current')?.textContent

// Проверить все цены
document.querySelectorAll('[class*="price"]')

// Проверить характеристики
document.querySelectorAll('.specifications tr')
```

### Шаг 5: Обновить код
После нахождения актуальных селекторов, обновите:
- `VoltCategoryParser.cs` - строки 45-70 (селекторы категорий)
- `VoltProductParser.cs` - строки 100-250 (селекторы товаров)

---

## Шаблоны для проверки

### Заголовок товара
```javascript
// Проверить все h1
document.querySelectorAll('h1').forEach(h => console.log(h.textContent?.trim()))

// Проверить с классом
document.querySelector('h1[class*="title"]')?.textContent
document.querySelector('[itemprop="name"]')?.textContent
```

### Цена
```javascript
// Все элементы с price в классе
document.querySelectorAll('[class*="price"]').forEach(el => {
    console.log(el.className, ':', el.textContent?.trim())
})

// Проверить microdata
document.querySelectorAll('[itemprop="price"]').forEach(el => {
    console.log('price:', el.getAttribute('content'), el.textContent)
})
```

### Характеристики
```javascript
// Таблица характеристик
const table = document.querySelector('.specifications, .product-attributes, table')
if (table) {
    const rows = table.querySelectorAll('tr')
    rows.forEach(row => {
        const cells = row.querySelectorAll('td, th')
        if (cells.length >= 2) {
            console.log(cells[0].textContent, ':', cells[1].textContent)
        }
    })
}
```

### Бренд
```javascript
// Проверить все возможные места
console.log('Brand classes:', document.querySelectorAll('[class*="brand"]'))
console.log('Brand microdata:', document.querySelector('[itemprop="brand"]'))

// Поиск в тексте
const text = document.body.textContent
const brandMatch = text.match(/Бренд[:\s]+([^\n]+)/i)
console.log('Brand from text:', brandMatch?.[1])
```

---

## Типичные селекторы для 220-volt.ru

На основе анализа структуры сайта, вот предполагаемые селекторы (ТРЕБУЮТ ПРОВЕРКИ):

### Карточка товара в каталоге
```
.catalog-item, .goods-item, .product-card, .catalog-product
```

### Ссылка на товар
```
.catalog-item a[href*="/catalog-"], .goods-item a[href*="/catalog-"]
```

### Название в карточке
```
.catalog-item .title, .goods-item .product-name, .catalog-item h3 a
```

### Цена в карточке
```
.catalog-item .price, .goods-item .goods-price, [itemprop="price"]
```

### На странице товара

**Заголовок:**
```
h1.product-title, h1[itemprop="name"], .goods-header h1
```

**Цена:**
```
.price-current, .product-price, [itemprop="price"], .goods-price
```

**Старая цена:**
```
.price-old, .old-price, .previous-price
```

**Бренд:**
```
.product-brand, .brand-value, [itemprop="brand"]
```

**Характеристики:**
```
.specifications table, .product-attributes, .characteristics-table
```

**Наличие:**
```
.availability, .stock-status, .goods-availability
```

**Артикул:**
```
.sku, .product-sku, [itemprop="sku"]
```

---

## Быстрая проверка через консоль браузера

Откройте консоль и выполните:

```javascript
// 1. Найти все уникальные классы элементов с "price"
const priceClasses = [...document.querySelectorAll('[class*="price"]')]
    .map(el => el.className)
    .filter((v, i, a) => a.indexOf(v) === i);
console.log('Price classes:', priceClasses);

// 2. Найти все уникальные классы элементов с "title"  
const titleClasses = [...document.querySelectorAll('[class*="title"], h1')]
    .map(el => el.className || el.tagName)
    .filter((v, i, a) => a.indexOf(v) === i);
console.log('Title elements:', titleClasses);

// 3. Найти структуру таблицы характеристик
const specTables = document.querySelectorAll('table.specifications, table.product-attributes, .specifications, .product-attributes');
console.log('Spec tables found:', specTables.length);
if (specTables.length > 0) {
    console.log('First table classes:', specTables[0].className);
    console.log('First table HTML:', specTables[0].outerHTML.substring(0, 500));
}
```

Результаты скопируйте и используйте для обновления селекторов в коде.

---

## Контакты и ссылки

- Основной сайт: https://www.220-volt.ru/
- Каталог электроинструментов: https://www.220-volt.ru/catalog-9889-elektroinstrumenty/
- Дрели (пример): https://www.220-volt.ru/catalog-10125-dreti/
