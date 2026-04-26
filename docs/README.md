# Vseinstrumenti Parser — Documentation Index

## 📚 Быстрый старт

### Начать работу
1. **Установка .NET 8.0** - [скачать](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Клонировать репозиторий**
3. **Запустить**: `dotnet run`
4. **Проверить**: `http://localhost:5000/health`

---

## 📖 Документация

### Для разработчиков
| Документ | Описание |
|----------|----------|
| [VOLT_SELECTORS_UPDATE_SUMMARY.md](./VOLT_SELECTORS_UPDATE_SUMMARY.md) | **Начни здесь!** Резюме обновлений селекторов 220-volt.ru |
| [VOLT_SELECTORS_REFERENCE.md](./VOLT_SELECTORS_REFERENCE.md) | Полный справочник CSS-селекторов для 220-volt.ru |
| [TESTING_VOLT_SELECTORS.md](./TESTING_VOLT_SELECTORS.md) | Пошаговая инструкция по тестированию через DevTools |
| [VOLT_SELECTORS_ANALYSIS.md](./VOLT_SELECTORS_ANALYSIS.md) | Анализ структуры сайта с примерами скриптов |

### Архитектура
- `Services/` - Бизнес-логика (парсеры, клиенты)
- `Interfaces/` - Контракты сервисов
- `Models/` - DTO и конфигурации
- `Controllers/` - API эндпоинты

### API Reference
- `GET /api/categories/vseinstrumenti` - Получить категории
- `GET /api/categories/volt` - Получить категории 220-volt.ru
- `GET /api/products/vseinstrumenti` - Получить товары
- `GET /api/products/volt` - Получить товары 220-volt.ru
- `GET /health` - Health check
- `GET /health/ready` - Ready check
- `GET /health/live` - Liveness check

---

## 🔧 Конфигурация

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": "Information"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "RequestSettings": {
    "TimeoutSeconds": 30,
    "UserAgent": "Mozilla/5.0...",
    "DelayBetweenRequestsMs": 1000
  },
  "CacheSettings": {
    "ExpirationMinutes": 60,
    "MaxItems": 1000
  }
}
```

### Переменные окружения
```bash
# Serilog
Serilog__MinimumLevel__Default=Information

# Redis
Redis__ConnectionString=localhost:6379

# Seq (логирование)
Seq__Url=http://localhost:5341

# Prometheus
Metrics__Enabled=true
```

---

## 🧪 Тестирование

### Запуск тестов
```bash
dotnet test
```

### Проверка кода
```bash
dotnet format
dotnet analyze
```

### Тестирование селекторов
```bash
# PowerShell скрипт
powershell -ExecutionPolicy Bypass -File scripts/test-volt-selectors.ps1

# Анализатор HTML
dotnet run --project tests/VoltSelectorAnalyzer/
```

---

## 🐳 Docker

### Запуск через docker-compose
```bash
# Все сервисы
docker-compose up -d

# Только парсер
docker-compose up -d vseinstrumenti-parser

# Логи
docker-compose logs -f vseinstrumenti-parser
```

### Сервисы в docker-compose
| Сервис | Порт | Описание |
|--------|------|----------|
| vseinstrumenti-parser | 5000 | Основное приложение |
| redis | 6379 | Кэш |
| seq | 5341 | Логирование |
| prometheus | 9090 | Метрики |
| grafana | 3000 | Дашборды |
| otel-collector | 4317 | OpenTelemetry |

---

## 📊 Мониторинг

### Prometheus метрики
- `parser_requests_total` - Всего запросов
- `parser_success_count` - Успешных парсингов
- `parser_error_count` - Ошибок парсинга
- `parser_duration_seconds` - Время парсинга
- `cache_hits_total` - Попаданий в кэш
- `cache_misses_total` - Пропусков кэша

### Grafana дашборды
- **Parser Overview** - Общий статус парсера
- **Error Rates** - Ошибки по типам
- **Performance** - Производительность
- **Cache Statistics** - Статистика кэша

### Health Checks
```bash
# Проверка доступности
curl http://localhost:5000/health/live

# Проверка готовности
curl http://localhost:5000/health/ready

# Детальная информация
curl http://localhost:5000/health
```

---

## 🛠 CI/CD

### GitHub Actions
- **build** - Сборка проекта
- **test** - Запуск тестов
- **publish** - Публикация артефактов
- **deploy** - Деплой на сервер

### Логи
```bash
# Логи в консоль
docker-compose logs vseinstrumenti-parser

# Логи в Seq
http://localhost:5341/events

# Логи в файл
logs/app-.log
```

---

## 📝 Таск-трек

### 🔴 Высокий приоритет
- [x] Адаптация селекторов 220-volt.ru
- [ ] Нормализация данных из разных источников
- [ ] Объединённый API для сравнения цен

### 🟡 Средний приоритет
- [ ] Поддержка прокси-ротации
- [ ] Расширение алертинга
- [ ] Оптимизация производительности

### 🟢 Низкий приоритет
- [ ] Веб-интерфейс
- [ ] Экспорт в XML/PostgreSQL

---

## 🆘 Troubleshooting

### Проблема: Парсер возвращает пустые результаты
**Решение:**
1. Проверьте селекторы в `docs/TESTING_VOLT_SELECTORS.md`
2. Обновите селекторы в `VoltProductParser.cs`
3. Проверьте, что сайт доступен и нет CAPTCHA

### Проблема: Ошибки подключения к Redis
**Решение:**
```bash
# Проверьте, что Redis запущен
docker-compose ps

# Перезапустите Redis
docker-compose restart redis

# Проверьте лог
docker-compose logs redis
```

### Проблема: Ошибки компиляции
**Решение:**
```bash
# Очистка и пересборка
dotnet clean
dotnet restore
dotnet build

# Проверка зависимостей
dotnet list package
```

---

## 📞 Поддержка

- **Команда**: NLP-Core-Team
- **Репозиторий**: `vseinstrumenti-parser`
- **Проблемы**: GitHub Issues
- **Документация**: Этот файл + `docs/`

---

**Последнее обновление**: 2024
**Версия**: 2.0
