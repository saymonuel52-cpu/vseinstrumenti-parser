# Инструкция по тестированию селекторов 220-volt.ru

## 🎯 Цель
Проверить и скорректировать CSS-селекторы парсера для сайта 220-volt.ru

---

## 📋 Шаг 1: Открыть сайт в браузере

Откройте Chrome или Firefox и перейдите на:
- **Главная**: https://www.220-volt.ru/
- **Категория**: https://www.220-volt.ru/catalog-9889-elektroinstrumenty/
- **Товар (пример)**: https://www.220-volt.ru/catalog-10125-dreti/

---

## 🔍 Шаг 2: Открыть DevTools

**Способы открыть:**
- Нажать `F12`
- ПКМ на любом элементе → "Просмотреть код" / "Исследовать элемент"
- `Ctrl + Shift + I` (Windows/Linux) или `Cmd + Opt + I` (Mac)

---

## 🧪 Шаг 3: Проверить селекторы через Console

### 3.1. Открыть Console
- Нажмите вкладку **Console** в DevTools
- Или `Ctrl + Shift + J` (Windows/Linux) / `Cmd + Opt + J` (Mac)

### 3.2. Вставить скрипт проверки

```javascript
// ====================================
// ПРОВЕРКА СЕЛЕКТОРОВ 220-VOLT.RU
// ====================================

console.log("=== Анализ структуры 220-volt.ru ===\n");

// 1. ЗАГОЛОВОК ТОВАРА
console.log("1️⃣ ЗАГОЛОВОК:");
const h1Selectors = ['h1.product-title', 'h1[itemprop="name"]', '.goods-header h1', 'h1'];
h1Selectors.forEach(sel => {
    const el = document.querySelector(sel);
    if (el) {
        console.log(`  [OK] ${sel} -> "${el.textContent.trim().substring(0, 50)}..."`);
    } else {
        console.log(`  [✗] ${sel} -> НЕ НАЙДЕНО`);
    }
});

// 2. ЦЕНА
console.log("\n2️⃣ ЦЕНА:");
const priceClasses = [...document.querySelectorAll('[class*="price"]')]
    .map(el => ({ class: el.className, text: el.textContent.trim() }))
    .slice(0, 5);
priceClasses.forEach(p => {
    console.log(`  ${p.class} -> ${p.text}`);
});

// 3. БРЕНД
console.log("\n3️⃣ БРЕНД:");
const brandSelectors = ['.brand-value', '.product-brand', '[itemprop="brand"]'];
brandSelectors.forEach(sel => {
    const el = document.querySelector(sel);
    if (el) {
        console.log(`  [OK] ${sel} -> "${el.textContent.trim()}"`);
    }
});

// 4. ХАРАКТЕРИСТИКИ
console.log("\n4️⃣ ХАРАКТЕРИСТИКИ:");
const specSelectors = ['.specifications', '.product-attributes', '.characteristics-table'];
specSelectors.forEach(sel => {
    const el = document.querySelector(sel);
    if (el) {
        const rows = el.querySelectorAll('tr');
        console.log(`  [OK] ${sel} -> ${rows.length} строк`);
        
        // Показать первые 3 характеристики
        rows.slice(0, 3).forEach(row => {
            const cells = row.querySelectorAll('td, th');
            if (cells.length >= 2) {
                console.log(`      • ${cells[0].textContent.trim()} : ${cells[1].textContent.trim()}`);
            }
        });
    }
});

// 5. НАЛИЧИЕ
console.log("\n5️⃣ НАЛИЧИЕ:");
const availSelectors = ['.availability', '.stock-status', '.goods-availability'];
availSelectors.forEach(sel => {
    const el = document.querySelector(sel);
    if (el) {
        console.log(`  [OK] ${sel} -> "${el.textContent.trim()}"`);
    }
});

// 6. АРТИКУЛ
console.log("\n6️⃣ АРТИКУЛ:");
const skuSelectors = ['.sku', '.product-sku', '[itemprop="sku"]'];
skuSelectors.forEach(sel => {
    const el = document.querySelector(sel);
    if (el) {
        console.log(`  [OK] ${sel} -> "${el.textContent.trim()}"`);
    }
});

// 7. ХЛЕБНЫЕ КРОШКИ
console.log("\n7️⃣ ХЛЕБНЫЕ КРОШКИ:");
const breadcrumbLinks = document.querySelectorAll('.breadcrumbs a');
if (breadcrumbLinks.length > 0) {
    console.log(`  Найдено ${breadcrumbLinks.length} ссылок:`);
    breadcrumbLinks.forEach((link, i) => {
        console.log(`    [${i}] ${link.textContent.trim()} -> ${link.href}`);
    });
}

// 8. ВСЕ УНИКАЛЬНЫЕ CLASS-АТРИБУТЫ С "PRICE"
console.log("\n8️⃣ ВСЕ PRICE-КЛАССЫ:");
const allPriceClasses = [...new Set(
    [...document.querySelectorAll('[class*="price"]')]
        .map(el => el.className)
)]
.slice(0, 10);
allPriceClasses.forEach(cls => console.log(`  • ${cls}`));

console.log("\n=== Готово! ===");
console.log("Скопируйте результаты и используйте для обновления селекторов в коде.");
```

### 3.3. Скопировать результаты
- Выделите весь вывод в Console
- `Ctrl + C` для копирования
- Сохраните в файл или вставьте в чат

---

## 📸 Шаг 4: Скриншоты (опционально)

Если результаты неочевидны, сделайте скриншоты:

1. **DevTools Elements панель** - показать структуру HTML нужного элемента
2. **Выделенный элемент** - подсветка на странице

**Как сделать:**
- ПКМ на элементе в Elements → Capture node screenshot

---

## 🛠 Шаг 5: Обновить код

### Если селекторы отличаются от ожидаемых:

1. Откройте файл:
   - `Services/VoltProductParser.cs` - для товаров
   - `Services/VoltCategoryParser.cs` - для категорий

2. Найдите соответствующий метод:
   - `ExtractProductName()` - заголовок
   - `ExtractPrice()` - цена
   - `ExtractBrand()` - бренд
   - `ExtractSpecifications()` - характеристики
   - и т.д.

3. Обновите селекторы в массиве `selectors`:
```csharp
var selectors = new[]
{
    "ВАШ_НОВЫЙ_СЕЛЕКТОР",  // Приоритет 1
    "АЛЬТЕРНАТИВНЫЙ",       // Приоритет 2
    "FALLBACK"              // Если ничего не нашлось
};
```

4. Сохраните файл

---

## 🧪 Шаг 6: Протестировать изменения

### Локальный запуск:
```bash
# В корневой папке проекта
dotnet run
```

### Тестовый запрос:
```bash
# Если приложение запущено
curl http://localhost:5000/api/products/volt?url=https://www.220-volt.ru/catalog-10125-dreti/[ID_ТОВАРА]
```

### Проверить результат:
- Откройте `bin/Debug/net8.0/output/` (или другой выходной каталог)
- Проверьте JSON/CSV файлы с результатами парсинга

---

## ✅ Чек-лист проверки

- [ ] Заголовок товара извлекается правильно
- [ ] Цена (текущая и старая) корректная
- [ ] Бренд определяется верно
- [ ] Характеристики извлекаются все
- [ ] Наличие определяется корректно
- [ ] Артикул не пустой
- [ ] Категория берётся из хлебных крошек

---

## 📞 Помощь

Если что-то не работает:
1. Проверьте, что сайт доступен (откройте в браузере)
2. Проверьте, что нет CAPTCHA или блокировки
3. Убедитесь, что User-Agent настроен правильно
4. Попробуйте другой товар/категорию

---

## 📝 Пример результата проверки

```
=== Анализ структуры 220-volt.ru ===

1️⃣ ЗАГОЛОВОК:
  [OK] h1.product-title -> "Дрель ударная Bosch PSB 1800 LI-2..."
  [OK] h1[itemprop="name"] -> "Дрель ударная Bosch PSB 1800 LI-2..."

2️⃣ ЦЕНА:
  price-current -> 5 490 ₽
  price-old -> 7 990 ₽

3️⃣ БРЕНД:
  [OK] .brand-value -> "Bosch"

4️⃣ ХАРАКТЕРИСТИКИ:
  [OK] .specifications -> 15 строк
      • Мощность : 18 В
      • Ёмкость аккумулятора : 2.0 Ач
      • Патрон : 1.5-13 мм

5️⃣ НАЛИЧИЕ:
  [OK] .availability -> "В наличии"

6️⃣ АРТИКУЛ:
  [OK] .sku -> "06019H6100"

7️⃣ ХЛЕБНЫЕ КРОШКИ:
  Найдено 3 ссылки:
    [0] Главная -> https://www.220-volt.ru/
    [1] Электроинструменты -> .../catalog-9889-elektroinstrumenty/
    [2] Дрели -> .../catalog-10125-dreti/

=== Готово! ===
```

Если видите `[OK]` - селектор работает. Если `[✗]` - нужно обновить селектор.

---

**Создано**: 2024
**Версия**: 1.0
