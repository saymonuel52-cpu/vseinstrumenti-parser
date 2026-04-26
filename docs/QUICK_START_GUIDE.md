# Быстрый старт - Запуск проекта

## 🚀 Запуск «одним нажатием»

### Windows (PowerShell)
```powershell
.\start.ps1
```

### Windows (CMD)
```cmd
start.bat
```

### Linux/macOS
```bash
chmod +x start.sh
./start.sh
```

---

## 📋 Параметры запуска

| Параметр | Описание | Пример |
|----------|----------|--------|
| `--rebuild` или `-r` | Пересобрать Docker образы без кэша | `.\start.ps1 -Rebuild` |
| `--no-browser` или `-n` | Не открывать браузер автоматически | `./start.sh --no-browser` |
| `--timeout` или `-t` | Время ожидания запуска (сек) | `./start.sh --timeout 180` |

---

## 🎯 Что делает скрипт

1. **Проверяет зависимости**
   - Docker и Docker Compose
   - .NET SDK (опционально)

2. **Останавливает старые контейнеры**
   ```bash
   docker-compose down --remove-orphans
   ```

3. **Запускает 8 сервисов**
   - `parser` - основной парсер
   - `webui` - Blazor веб-интерфейс
   - `redis` - кэш
   - `prometheus` - метрики
   - `grafana` - дашборды
   - `seq` - логи
   - `otel` - OpenTelemetry
   - `alertmanager` - уведомления

4. **Ожидает готовности** (до 120 сек)
   - Опрос health endpoint каждые 3 сек
   - Показывает прогресс в консоли

5. **Открывает браузер**
   - Автоматически открывает http://localhost:8082
   - Можно отключить флагом `--no-browser`

6. **Показывает информацию**
   - Все доступные сервисы
   - API ключ
   - Команды управления

---

## 📊 Доступные сервисы после запуска

| Сервис | URL | Описание |
|--------|-----|----------|
| **Web UI** | http://localhost:8082 | Blazor интерфейс |
| **API** | http://localhost:8080 | REST API |
| **Health Check** | http://localhost:8080/health | Статус сервисов |
| **Prometheus** | http://localhost:9090 | Метрики |
| **Grafana** | http://localhost:3000 | Дашборды |
| **Seq** | http://localhost:5341 | Логи |

---

## 🛠️ Управление контейнерами

### Остановить все сервисы
```bash
docker-compose down
```

### Перезапустить сервисы
```bash
docker-compose restart
```

### Просмотр логов
```bash
# Все сервисы
docker-compose logs -f

# Конкретный сервис
docker-compose logs -f webui
docker-compose logs -f parser
```

### Проверить статус
```bash
docker-compose ps
```

### Полная очистка
```bash
docker-compose down --volumes --remove-orphans
```

---

## 🔧 Решение проблем

### Ошибка: Docker не найден
**Решение**: Установите Docker Desktop
- Windows: https://www.docker.com/products/docker-desktop
- macOS: `brew install --cask docker`
- Ubuntu: `sudo apt-get install docker.io`

### Ошибка: Порт 8080 занят
**Решение**: Остановите приложение, использующее порт
```bash
# Windows
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Linux/macOS
lsof -ti:8080 | xargs kill -9
```

### Ошибка: Таймаут ожидания
**Решение**: Увеличьте время ожидания
```bash
.\start.ps1 -Timeout 180
./start.sh --timeout 180
```

### Ошибка: Недостаточно памяти
**Решение**: Увеличьте лимиты Docker
- Docker Desktop → Settings → Resources
- Рекомендовано: 4GB+ RAM, 2+ CPU cores

---

## 💡 Полезные команды

### Проверка здоровья
```bash
curl http://localhost:8080/health
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

### Просмотр метрик
```bash
curl http://localhost:9090/api/v1/query?query=up
```

### Тест API
```bash
# Получить статус
curl -H "X-API-Key: your-api-key" http://localhost:8080/api/status

# Запустить парсинг
curl -X POST -H "X-API-Key: your-api-key" http://localhost:8080/api/parse
```

---

## 🎨 Примеры использования

### Стандартный запуск
```bash
.\start.ps1
```
**Результат**: Все сервисы запускаются, открывается браузер

### Пересборка образов
```bash
.\start.ps1 -Rebuild
```
**Результат**: Образы пересобираются без кэша, затем запускаются

### Без браузера
```bash
./start.sh --no-browser
```
**Результат**: Сервисы запускаются, браузер не открывается

### С увеличенным таймаутом
```bash
./start.sh --timeout 180
```
**Результат**: Ожидание готовности до 3 минут

### Комбинированный запуск
```bash
.\start.ps1 -Rebuild -NoBrowser -Timeout 180
```
**Результат**: Пересборка + запуск + без браузера + 3 мин таймаут

---

## 📈 Мониторинг после запуска

### Проверить, что всё работает
```bash
# Health check
curl http://localhost:8080/health

# Web UI
Start-Process http://localhost:8082

# Логи в реальном времени
docker-compose logs -f
```

### Графана дашборды
1. Откройте http://localhost:3000
2. Логин: `admin` / `admin`
3. Добавьте Prometheus как источник данных
4. Импортируйте дашборды из `monitoring/dashboards/`

### Seq логи
1. Откройте http://localhost:5341
2. Настройте API ключ (если требуется)
3. Просматривайте логи в реальном времени

---

## 🔄 Обновление проекта

### Получить последние изменения
```bash
git pull origin main
```

### Пересобрать и перезапустить
```bash
.\start.ps1 -Rebuild
```

### Очистить всё и начать заново
```bash
docker-compose down --volumes --remove-orphans
.\start.ps1 -Rebuild
```

---

## ⚙️ Кастомизация

### Изменить порт Web UI
В `docker-compose.yml`:
```yaml
ports:
  - "8082:8082"  # Измените 8082 на другой порт
```

### Изменить API Key
В `ParserWebUI/appsettings.json`:
```json
{
  "ApiKey": "your-new-secure-key"
}
```

### Изменить таймауты
В `docker-compose.yml` для сервиса `parser`:
```yaml
environment:
  - HttpClientSettings__TimeoutSeconds=60
```

---

## 📞 Поддержка

Если скрипт не работает:

1. Проверьте версии:
   ```bash
   docker --version
   docker-compose --version
   ```

2. Запустите с детальным выводом:
   ```bash
   docker-compose up -d --verbose
   ```

3. Проверьте логи:
   ```bash
   docker-compose logs -f --tail=100
   ```

4. Создайте issue на GitHub с логами

---

**Готово! Ваш проект запущен и готов к работе! 🎉**
