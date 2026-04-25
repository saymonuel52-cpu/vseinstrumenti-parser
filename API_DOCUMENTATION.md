# Документация API Vseinstrumenti Parser

## Общая информация

**Базовый URL**: `http://localhost:8080` (или адрес вашего развертывания)

**Формат данных**: JSON

**Аутентификация**: В текущей версии не требуется (можно настроить через конфигурацию)

## Эндпоинты

### 1. Информация о сервисе

#### `GET /`
Возвращает HTML-страницу с информацией о сервисе и списком доступных эндпоинтов.

**Ответ**: HTML страница

### 2. Health Checks

#### `GET /health`
Полная проверка здоровья приложения. Проверяет все компоненты: систему, Redis, внешние сайты.

**Ответ**:
```json
{
  "status": "Healthy",
  "totalDuration": 0.123,
  "entries": [
    {
      "name": "system_resources",
      "status": "Healthy",
      "duration": 0.001,
      "description": "System resources OK. Memory: 2048MB available, Disk: 50GB free"
    }
  ]
}
```

**Статусы**:
- `Healthy` - все компоненты работают нормально
- `Degraded` - некоторые компоненты работают с ограничениями
- `Unhealthy` - критические компоненты не работают

#### `GET /health/ready`
Проверка готовности к работе. Проверяет только критически важные компоненты.

#### `GET /health/live`
Проверка живости. Базовая проверка, что приложение запущено.

### 3. Метрики

#### `GET /metrics`
Метрики в формате Prometheus для мониторинга.

**Ответ**: Текстовый формат Prometheus
```
# Метрики парсера vseinstrumenti.ru
# Время: 2026-04-25T16:30:00Z
parser_uptime_seconds 12345.67
```

### 4. API парсинга

#### `GET /api/categories`
Получить список всех категорий электроинструментов.

**Ответ**:
```json
[
  {
    "id": "elektroinstrument",
    "name": "Электроинструмент",
    "url": "https://www.vseinstrumenti.ru/elektroinstrument/",
    "productCount": 1250,
    "subcategories": [
      {
        "id": "dreli-shurupoverty",
        "name": "Дрели и шуруповерты",
        "url": "https://www.vseinstrumenti.ru/elektroinstrument/dreli-shurupoverty/"
      }
    ]
  }
]
```

#### `GET /api/products/{categoryUrl}`
Получить товары из указанной категории.

**Параметры**:
- `categoryUrl` (в пути) - URL категории (можно получить из `/api/categories`)
- `maxPages` (query, опционально) - максимальное количество страниц для парсинга (по умолчанию: 2)

**Пример**: `GET /api/products/https://www.vseinstrumenti.ru/elektroinstrument/dreli-shurupoverty/?maxPages=3`

**Ответ**:
```json
[
  {
    "id": "product-12345",
    "name": "Дрель-шуруповерт Bosch GSR 120-LI",
    "url": "https://www.vseinstrumenti.ru/...",
    "price": 4599,
    "oldPrice": 4999,
    "currency": "RUB",
    "brand": "Bosch",
    "availability": "В наличии",
    "rating": 4.5,
    "reviewCount": 24,
    "description": "Беспроводная дрель-шуруповерт...",
    "specifications": {
      "Мощность": "12 В",
      "Тип аккумулятора": "Li-Ion",
      "Крутящий момент": "30 Н·м"
    },
    "images": [
      "https://cdn.vseinstrumenti.ru/images/.../1.jpg"
    ]
  }
]
```

### 5. Swagger документация

#### `GET /api-docs`
Интерактивная документация Swagger UI (доступна только в режиме Development).

#### `GET /swagger/v1/swagger.json`
OpenAPI спецификация в формате JSON.

## Модели данных

### Category
```json
{
  "id": "string",
  "name": "string",
  "url": "string",
  "productCount": "number",
  "subcategories": "Category[]"
}
```

### Product
```json
{
  "id": "string",
  "name": "string",
  "url": "string",
  "price": "number",
  "oldPrice": "number",
  "currency": "string",
  "brand": "string",
  "availability": "string",
  "rating": "number",
  "reviewCount": "number",
  "description": "string",
  "specifications": "object",
  "images": "string[]",
  "parsedAt": "string (ISO 8601)"
}
```

### Health Report
```json
{
  "status": "string (Healthy|Degraded|Unhealthy)",
  "totalDuration": "number",
  "entries": [
    {
      "name": "string",
      "status": "string",
      "duration": "number",
      "description": "string",
      "exception": "string",
      "data": "object"
    }
  ]
}
```

## Коды ошибок

| Код | Описание |
|-----|----------|
| 200 | Успешный запрос |
| 400 | Неверные параметры запроса |
| 404 | Ресурс не найден |
| 429 | Слишком много запросов (rate limiting) |
| 500 | Внутренняя ошибка сервера |
| 502 | Ошибка при обращении к внешнему сайту |
| 503 | Сервис временно недоступен |

## Rate Limiting

По умолчанию включено ограничение скорости запросов:
- Максимум 100 запросов в минуту на IP-адрес
- При превышении лимита возвращается HTTP 429

Настройки можно изменить в `appsettings.Production.json`:
```json
{
  "Security": {
    "EnableRateLimiting": true,
    "RateLimitPermitLimit": 100,
    "RateLimitWindowSeconds": 60
  }
}
```

## Примеры использования

### Получение категорий с помощью curl
```bash
curl -X GET "http://localhost:8080/api/categories" \
  -H "Accept: application/json"
```

### Получение товаров из категории
```bash
curl -X GET "http://localhost:8080/api/products/https://www.vseinstrumenti.ru/elektroinstrument/dreli-shurupoverty/?maxPages=2" \
  -H "Accept: application/json"
```

### Проверка здоровья
```bash
curl -X GET "http://localhost:8080/health" \
  -H "Accept: application/json"
```

### Получение метрик Prometheus
```bash
curl -X GET "http://localhost:8080/metrics"
```

## Интеграция с мониторингом

### Prometheus
Сервис предоставляет метрики по адресу `/metrics` в формате Prometheus.

Конфигурация Prometheus:
```yaml
scrape_configs:
  - job_name: 'vseinstrumenti-parser'
    static_configs:
      - targets: ['vseinstrumenti-parser:8080']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

### Grafana
Используйте дашборд `grafana/dashboards/vseinstrumenti-parser-dashboard.json` для визуализации метрик.

### Health Checks для оркестраторов
Kubernetes, Docker Swarm и другие оркестраторы могут использовать эндпоинты:
- `/health/live` для liveness probe
- `/health/ready` для readiness probe

Пример для Kubernetes:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5
```

## Безопасность

### Аутентификация (опционально)
Для включения аутентификации установите в конфигурации:
```json
{
  "Security": {
    "EnableAuthentication": true,
    "AuthenticationScheme": "Bearer",
    "ApiKey": "your-secret-api-key"
  }
}
```

После этого все запросы должны содержать заголовок:
```
Authorization: Bearer your-secret-api-key
```

### CORS
По умолчанию CORS отключен. Для включения:
```json
{
  "Security": {
    "EnableCors": true,
    "CorsOrigins": ["https://example.com", "http://localhost:3000"]
  }
}
```

## Ограничения

1. **Rate Limiting**: Ограничение на количество запросов к внешним сайтам
2. **Кэширование**: Результаты кэшируются на 60 минут (настраивается)
3. **Таймауты**: Таймаут запросов 30 секунд (настраивается)
4. **Объем данных**: Максимум 100 товаров на категорию (настраивается)

## Логирование

Все запросы логируются с уровнем Information. Ошибки логируются с уровнем Error.

Логи доступны:
- В консоли (в режиме Development)
- В файлах `./logs/parser_*.log`
- В Seq (если настроено)
- В OpenTelemetry (если настроено)

## Версионирование

Текущая версия API: v1.0.0

Изменения в API будут сопровождаться увеличением версии и обратной совместимостью в пределах мажорной версии.

## Поддержка

Для вопросов и сообщений об ошибках:
- GitHub Issues: [ссылка на репозиторий]
- Email: dev@example.com
- Документация: `/api-docs` (в режиме Development)