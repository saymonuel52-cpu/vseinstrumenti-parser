# Vseinstrumenti Parser Web UI — Руководство по развертыванию

## 📋 Содержание
1. [Требования](#требования)
2. [Быстрый старт](#быстрый-старт)
3. [Локальная разработка](#локальная-разработка)
4. [Production deployment](#production-deployment)
5. [API Key аутентификация](#api-key-аутентификация)
6. [Интеграция с существующим приложением](#интеграция-с-существующим-приложением)
7. [Troubleshooting](#troubleshooting)

---

## Требования

### Минимальные
- .NET 8.0 SDK
- Docker и Docker Compose
- 2GB RAM
- 10GB дискового пространства

### Рекомендуемые
- 4GB+ RAM
- Redis для кэширования
- Seq для централизованного логирования
- Prometheus + Grafana для мониторинга

---

## Быстрый старт

### 1. Клонировать и собрать
```bash
cd vseinstrumenti-parser
dotnet restore
dotnet build ParserWebUI/ParserWebUI.csproj
```

### 2. Запустить локально
```bash
cd ParserWebUI
dotnet run
```

Откройте браузер: `http://localhost:5000`

### 3. Запустить через Docker
```bash
docker-compose -f docker-compose.webui.yml up -d
```

Откройте: `http://localhost:5000`

---

## Локальная разработка

### Структура проекта
```
ParserWebUI/
├── Components/           # Blazor компоненты
│   ├── App.razor        # Корневой компонент
│   ├── Routes.razor     # Маршрутизация
│   └── Layout/          # Макеты страниц
│       └── MainLayout.razor
├── Pages/               # Страницы приложения
│   ├── Index.razor      # Дашборд
│   ├── Parse.razor      # Запуск парсинга
│   ├── Compare.razor    # Сравнение цен
│   ├── Logs.razor       # Логи
│   └── Settings.razor   # Настройки
├── Services/            # Сервисы
│   ├── ApiKeyMiddleware.cs
│   ├── ParseProgressService.cs
│   ├── ParserMonitoringService.cs
│   ├── ParseProgressHub.cs
│   └── DarkThemeService.cs
├── wwwroot/             # Статические файлы
│   ├── css/
│   └── js/
├── Program.cs           # Точка входа
└── appsettings.json     # Конфигурация
```

### Запуск в режиме разработки
```bash
# Установить переменную окружения
export ASPNETCORE_ENVIRONMENT=Development

# Запустить с hot reload
dotnet watch run --project ParserWebUI/ParserWebUI.csproj
```

### Настройка IDE (VS Code)
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch ParserWebUI",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/ParserWebUI/bin/Debug/net8.0/VseinstrumentiParser.WebUI.dll",
      "args": [],
      "cwd": "${workspaceFolder}/ParserWebUI",
      "stopAtEntry": false,
      "console": "internalConsole"
    }
  ]
}
```

---

## Production deployment

### Docker Compose (рекомендуется)

#### 1. Настроить переменные окружения
```bash
# Создайте .env файл
cat > .env << EOF
WEBUI_API_KEY=your-secure-api-key-here
ASPNETCORE_ENVIRONMENT=Production
REDIS_CONNECTION_STRING=redis:6379
SEQ_URL=http://seq:5341
EOF
```

#### 2. Запустить все сервисы
```bash
docker-compose -f docker-compose.webui.yml up -d
```

#### 3. Проверить статус
```bash
docker-compose -f docker-compose.webui.yml ps
```

#### 4. Просмотр логов
```bash
docker-compose -f docker-compose.webui.yml logs -f webui
```

### Docker run (без Compose)
```bash
docker run -d \
  --name parser-webui \
  -p 5000:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Redis__ConnectionString=redis:6379 \
  -e ApiKey=your-api-key \
  --network parser-network \
  parser-webui:latest
```

### Kubernetes (опционально)
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: parser-webui
spec:
  replicas: 3
  selector:
    matchLabels:
      app: parser-webui
  template:
    metadata:
      labels:
        app: parser-webui
    spec:
      containers:
      - name: webui
        image: parser-webui:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ApiKey
          valueFrom:
            secretKeyRef:
              name: parser-secrets
              key: webui-api-key
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: parser-webui-service
spec:
  selector:
    app: parser-webui
  ports:
  - port: 80
    targetPort: 8080
  type: LoadBalancer
```

---

## API Key аутентификация

### Генерация API ключа
```bash
# Генерировать случайный ключ
openssl rand -hex 32

# Или через .NET
dotnet run --project ParserWebUI -- --generate-api-key
```

### Настройка в appsettings.json
```json
{
  "ApiKey": "your-generated-api-key-here",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Использование API ключа

#### В заголовке (рекомендуется)
```bash
curl -H "X-API-Key: your-api-key" http://localhost:5000/api/metrics
```

#### В query string
```bash
curl "http://localhost:5000/api/metrics?api_key=your-api-key"
```

### Смена API ключа
1. Сгенерировать новый ключ
2. Обновить в `appsettings.json` или переменной окружения
3. Перезапустить контейнер
```bash
docker-compose restart webui
```

---

## Интеграция с существующим приложением

### Вариант 1: Совместный запуск (рекомендуется)
```yaml
# docker-compose.yml
version: '3.8'

services:
  parser-api:
    build: .
    ports:
      - "8080:8080"
    
  webui:
    build:
      context: .
      dockerfile: ParserWebUI/Dockerfile
    ports:
      - "5000:8080"
    depends_on:
      - parser-api
```

### Вариант 2: Standalone Web UI
Web UI может работать отдельно, используя существующие сервисы через DI:
```csharp
// Program.cs в ParserWebUI
builder.Services.AddSingleton<VseinstrumentiParserService>();
builder.Services.AddSingleton<VoltCategoryParser>();
builder.Services.AddSingleton<VoltProductParser>();
```

### Общие зависимости
```yaml
# Общие сервисы для API и Web UI
redis:
  image: redis:7-alpine
  
seq:
  image: datalust/seq:latest
  
prometheus:
  image: prom/prometheus
  
grafana:
  image: grafana/grafana
```

---

## Страницы Web UI

### 📊 Дашборд (`/`)
- Статус системы (health check)
- Ключевые метрики
- Активные задачи
- Статус зависимостей

### 🚀 Запуск парсинга (`/parse`)
- Выбор источника (vseinstrumenti.ru / 220-volt.ru)
- Фильтрация по категории
- Прогресс в реальном времени
- Логи выполнения
- Результаты и экспорт

### 🔍 Сравнение цен (`/compare`)
- Поиск товаров
- Сравнение между источниками
- Фильтры и сортировка
- Кэширование результатов

### 📋 Логи (`/logs`)
- Интеграция с Seq
- Фильтрация по уровню/времени
- Поиск по тексту
- Экспорт логов

### ⚙️ Настройки (`/settings`)
- Управление кэшем
- Настройка алертов (Telegram/Slack/Email)
- Редактирование селекторов
- Экспорт/импорт конфигурации

---

## Monitoring & Observability

### Health Checks
```bash
# Проверка доступности
curl http://localhost:5000/health/live

# Проверка готовности
curl http://localhost:5000/health/ready

# Детальная информация
curl http://localhost:5000/health
```

### Метрики (Prometheus)
```bash
curl http://localhost:5000/metrics
```

### Логи (Seq)
```
http://localhost:5341
```

---

## Troubleshooting

### Проблема: Web UI не запускается
**Решение:**
```bash
# Проверить логи
docker-compose logs webui

# Проверить порт занят ли
netstat -tlnp | grep 5000

# Пересобрать образ
docker-compose build --no-cache webui
```

### Проблема: Ошибки подключения к Redis
**Решение:**
```bash
# Проверить Redis запущен
docker-compose ps redis

# Проверить сеть
docker network inspect parser-network

# Перезапустить Redis
docker-compose restart redis
```

### Проблема: Не работают селекторы парсера
**Решение:**
1. Перейдите в `/settings` → Конфигурация парсеров
2. Обновите CSS селекторы согласно документации `docs/VOLT_SELECTORS_REFERENCE.md`
3. Сохраните и протестируйте на `/parse`

### Проблема: Нет real-time обновлений
**Решение:**
```bash
# Проверить SignalR соединение
# Откройте DevTools → Console и Network
# Ищите подключения к /hubs/parse-progress

# Перезапустить SignalR hub
docker-compose restart webui
```

### Проблема: Медленная работа
**Решение:**
```bash
# Очистить кэш через Web UI → Settings
# Увеличить ресурсы контейнера
docker-compose up -d --cpus=2 --memory=2g webui

# Проверить нагрузку на Redis
docker exec -it parser-redis redis-cli info stats
```

---

## Безопасность

### Рекомендации для production:
1. **Всегда используйте HTTPS**
   ```bash
   # Настроить reverse proxy (nginx)
   # Или использовать ASP.NET Core Kestrel с SSL
   ```

2. **Защищайте API ключи**
   - Используйте Docker Secrets
   - Или Kubernetes Secrets
   - Никогда не коммитьте в Git

3. **Ограничьте доступ по сети**
   ```bash
   # Запустите только на localhost
   ASPNETCORE_URLS=http://127.0.0.1:8080
   ```

4. **Регулярно обновляйте зависимости**
   ```bash
   dotnet list package --outdated
   dotnet add package --version <latest>
   ```

---

## Обновление

### Перейти на новую версию
```bash
# Остановить текущую версию
docker-compose -f docker-compose.webui.yml down

# Собрать новую версию
docker-compose -f docker-compose.webui.yml build

# Запустить
docker-compose -f docker-compose.webui.yml up -d
```

### Откатить версию
```bash
# Использовать предыдущий тег
docker-compose -f docker-compose.webui.yml pull previous-tag
docker-compose -f docker-compose.webui.yml up -d
```

---

## Поддержка

- **Документация**: `docs/` папка
- **API**: `http://localhost:5000/swagger` (если включено)
- **Health**: `http://localhost:5000/health`
- **Команда**: NLP-Core-Team

---

**Версия**: 2.0  
**Последнее обновление**: 2024  
**Лицензия**: Proprietary
