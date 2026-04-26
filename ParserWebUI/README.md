# Vseinstrumenti Parser Web UI

Production-ready веб-интерфейс для управления парсингом, мониторинга и сравнения цен из интернет-магазинов.

## ✨ Возможности

### 📊 Дашборд
- Статус системы в реальном времени
- Ключевые метрики (товары, кэш, ошибки)
- Активные задачи парсинга
- Статус внешних зависимостей (Redis, Seq, Prometheus)

### 🚀 Управление парсингом
- Поддержка multiple источников (vseinstrumenti.ru, 220-volt.ru)
- Выбор категорий с автодополнением
- Настройки: лимиты, кэш, только новые товары
- Прогресс-бар в реальном времени (SignalR)
- Live-логи выполнения
- Экспорт результатов (CSV/JSON)

### 🔍 Сравнение цен
- Поиск по названию/бренду/категории
- Сравнение цен между источниками
- Фильтры: наличие, цена, бренд
- Сортировка по цене/разнице
- Кэширование результатов (15 мин)

### 📋 Логи и мониторинг
- Интеграция с Seq API
- Фильтрация по уровню/времени/источнику
- Поиск по тексту лога
- Статистика ошибок
- Экспорт логов

### ⚙️ Настройки
- Управление кэшем (просмотр, инвалидация)
- Настройка алертов (Telegram/Slack/Email)
- Редактирование CSS-селекторов
- Экспорт/импорт конфигурации
- Информация о системе

## 🎨 Особенности

- **Blazor Server** - единый C# код, real-time через SignalR
- **Bootstrap 5** - адаптивный дизайн, тёмная тема
- **API Key аутентификация** - защита от несанкционированного доступа
- **Production-ready** - health checks, логирование, мониторинг
- **Docker** - контейнеризация, docker-compose
- **Масштабируемость** - поддержка Kubernetes

## 🚀 Быстрый старт

### Локальный запуск
```bash
cd ParserWebUI
dotnet restore
dotnet run
```
Откройте: http://localhost:5000

### Docker Compose
```bash
docker-compose -f docker-compose.webui.yml up -d
```
Откройте: http://localhost:5000

## 📁 Структура проекта

```
ParserWebUI/
├── Components/
│   ├── App.razor              # Корневой компонент
│   ├── Routes.razor           # Маршрутизация
│   └── Layout/
│       └── MainLayout.razor   # Основной макет
├── Pages/
│   ├── Index.razor            # Дашборд
│   ├── Parse.razor            # Запуск парсинга
│   ├── Compare.razor          # Сравнение цен
│   ├── Logs.razor             # Логи
│   └── Settings.razor         # Настройки
├── Services/
│   ├── ApiKeyMiddleware.cs    # Аутентификация
│   ├── ParseProgressService.cs # Прогресс парсинга
│   ├── ParserMonitoringService.cs # Метрики
│   ├── ParseProgressHub.cs    # SignalR Hub
│   └── DarkThemeService.cs    # Тёмная тема
├── wwwroot/
│   ├── css/site.css           # Стили
│   └── js/site.js             # JavaScript
├── Program.cs                 # Точка входа
├── appsettings.json           # Конфигурация
└── Dockerfile                 # Docker образ
```

## 🔧 Конфигурация

### appsettings.json
```json
{
  "ApiKey": "your-api-key-here",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Seq": {
    "Url": "http://localhost:5341"
  },
  "Alerts": {
    "Telegram": {
      "Enabled": false,
      "BotToken": "",
      "ChatId": ""
    },
    "Slack": {
      "Enabled": false,
      "WebhookUrl": ""
    }
  }
}
```

### Переменные окружения
```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
Redis__ConnectionString=redis:6379
Seq__Url=http://seq:5341
ApiKey=your-secure-key
```

## 🔐 Безопасность

### API Key аутентификация

Все API-эндпоинты требуют ключ в заголовке:
```bash
curl -H "X-API-Key: your-key" http://localhost:5000/api/metrics
```

Или в query string:
```bash
curl "http://localhost:5000/api/metrics?api_key=your-key"
```

### Генерация ключа
```bash
openssl rand -hex 32
```

## 📊 Мониторинг

### Health Checks
```bash
# Liveness
curl http://localhost:5000/health/live

# Readiness
curl http://localhost:5000/health/ready

# Full status
curl http://localhost:5000/health
```

### Метрики Prometheus
```bash
curl http://localhost:5000/metrics
```

### Логи Seq
```
http://localhost:5341
```

## 🐳 Docker

### Сборка
```bash
docker build -t parser-webui -f ParserWebUI/Dockerfile .
```

### Запуск
```bash
docker run -d \
  -p 5000:8080 \
  -e ApiKey=your-key \
  --name parser-webui \
  parser-webui
```

### Docker Compose
```bash
docker-compose -f docker-compose.webui.yml up -d
```

## 🧪 Тестирование

### Запуск тестов
```bash
dotnet test ParserWebUI/
```

### Ручное тестирование
1. Откройте http://localhost:5000
2. Перейдите на `/parse`
3. Выберите источник и категорию
4. Запустите парсинг
5. Проверьте прогресс и логи
6. Перейдите на `/compare`
7. Проверьте сравнение цен

## 📈 Roadmap

### Сейчас
- ✅ Базовая функциональность
- ✅ SignalR real-time обновления
- ✅ Сравнение цен
- ✅ Управление кэшем

### В планах
- ⏳ Интеграция с PostgreSQL
- ⏳ Вебхуки для алертов
- ⏳ Многопользовательский доступ
- ⏳ Расширенные дашборды Grafana
- ⏳ Автоматическое обновление селекторов

## 🛠 Технологии

- **.NET 8.0** - runtime
- **Blazor Server** - UI framework
- **SignalR** - real-time updates
- **Bootstrap 5** - дизайн
- **Chart.js** - графики
- **Serilog** - логирование
- **Redis** - кэширование
- **Docker** - контейнеризация

## 📚 Документация

- [Deployment Guide](./DEPLOYMENT_GUIDE.md) - Развёртывание
- [API Documentation](../API_DOCUMENTATION.md) - API reference
- [Selectors Reference](../docs/VOLT_SELECTORS_REFERENCE.md) - CSS селекторы
- [Testing Guide](../docs/TESTING_VOLT_SELECTORS.md) - Тестирование

## 🤝 Контрибьюция

1. Fork проект
2. Создайте feature branch
3. Commit изменения
4. Push к branch
5. Создайте Pull Request

## 📝 Лицензия

Proprietary. Все права защищены.

## 📞 Контакты

- **Команда**: NLP-Core-Team
- **Проект**: vseinstrumenti-parser
- **Версия**: 2.0

---

**Последнее обновление**: 2024  
**Статус**: Production Ready ✅
