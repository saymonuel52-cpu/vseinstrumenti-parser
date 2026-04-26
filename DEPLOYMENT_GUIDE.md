# Руководство по деплою и тестированию

## 🔧 Важные моменты перед деплоем

### 1. Проверка и настройка CI/CD пайплайна

Файл `.github/workflows/ci-cd.yml` содержит полный пайплайн, но требует настройки:

#### Секция deploy (строки 183-191):
```yaml
- name: Deploy to server (example)
  run: |
    echo "Deployment would happen here"
    # Пример для SSH деплоя:
    # scp -r ./publish/linux-x64/* user@server:/opt/vseinstrumenti-parser/
    # ssh user@server "systemctl restart vseinstrumenti-parser"
```

**Замените на реальный деплой-скрипт под выбранную платформу:**

#### Вариант A: Деплой на VPS/VM через SSH
```yaml
- name: Deploy to production server
  run: |
    # Создаем директорию для деплоя
    mkdir -p ~/.ssh
    echo "${{ secrets.DEPLOY_SSH_KEY }}" > ~/.ssh/id_rsa
    chmod 600 ~/.ssh/id_rsa
    
    # Копируем файлы на сервер
    scp -o StrictHostKeyChecking=no -r ./publish/linux-x64/* \
      ${{ secrets.DEPLOY_USER }}@${{ secrets.DEPLOY_HOST }}:/opt/vseinstrumenti-parser/
    
    # Перезапускаем сервис
    ssh -o StrictHostKeyChecking=no \
      ${{ secrets.DEPLOY_USER }}@${{ secrets.DEPLOY_HOST }} \
      "sudo systemctl restart vseinstrumenti-parser"
```

#### Вариант B: Деплой в Docker Registry + Kubernetes
```yaml
- name: Build and push Docker image
  uses: docker/build-push-action@v5
  with:
    context: .
    push: true
    tags: |
      ${{ secrets.DOCKERHUB_USERNAME }}/vseinstrumenti-parser:latest
      ${{ secrets.DOCKERHUB_USERNAME }}/vseinstrumenti-parser:${{ github.sha }}

- name: Deploy to Kubernetes
  run: |
    kubectl set image deployment/vseinstrumenti-parser \
      vseinstrumenti-parser=${{ secrets.DOCKERHUB_USERNAME }}/vseinstrumenti-parser:${{ github.sha }}
    kubectl rollout status deployment/vseinstrumenti-parser
```

#### Вариант C: Деплой на Azure App Service
```yaml
- name: Deploy to Azure Web App
  uses: azure/webapps-deploy@v2
  with:
    app-name: 'vseinstrumenti-parser'
    slot-name: 'production'
    publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
    package: ./publish/linux-x64
```

### 2. Настройка GitHub Secrets

В репозитории: **Settings → Secrets and variables → Actions** добавьте:

#### Для SSH деплоя:
```
DEPLOY_SSH_KEY        # Приватный SSH ключ для доступа к серверу
DEPLOY_USER           # Имя пользователя на сервере (например, ubuntu)
DEPLOY_HOST           # IP адрес или домен сервера
```

#### Для Docker деплоя:
```
DOCKERHUB_USERNAME    # Имя пользователя Docker Hub
DOCKERHUB_TOKEN       # Токен доступа Docker Hub (Settings → Security → Access Tokens)
```

#### Для уведомлений:
```
SLACK_WEBHOOK         # Webhook URL для Slack уведомлений
TELEGRAM_BOT_TOKEN    # Токен Telegram бота (опционально)
TELEGRAM_CHAT_ID      # ID чата Telegram (опционально)
```

#### Для мониторинга (опционально):
```
PROMETHEUS_URL        # URL Prometheus сервера
GRAFANA_API_KEY       # API ключ Grafana для автоматического импорта дашбордов
```

### 3. Протестируйте health check после деплоя

После успешного деплоя проверьте работоспособность:

```bash
# Проверка health check
curl -f https://your-domain.com/health

# Проверка readiness
curl -f https://your-domain.com/health/ready

# Проверка liveness
curl -f https://your-domain.com/health/live

# Получение метрик
curl https://your-domain.com/metrics

# Проверка API
curl https://your-domain.com/api/categories
```

## 🎁 Бонус: быстрый тест локально перед деплоем

Перед деплоем в production протестируйте приложение локально:

```bash
# 1. Запустите полный стек мониторинга
docker-compose up -d

# 2. Проверьте health checks
curl -f http://localhost:8080/health

# 3. Запустите интеграционные тесты
dotnet test --filter "Category=Integration"

# 4. Проверьте метрики Prometheus
curl http://localhost:9090/targets  # Должен видеть vseinstrumenti-parser:8080

# 5. Проверьте логи в Seq
open http://localhost:8081

# 6. Проверьте дашборд Grafana
open http://localhost:3000
# Логин: admin / admin

# 7. Запустите нагрузочное тестирование (опционально)
docker run --rm -it --network host \
  alpine/bombardier -c 10 -d 30s -l http://localhost:8080/health

# 8. Проверьте graceful shutdown
docker-compose stop vseinstrumenti-parser
# В логах должны быть сообщения о корректном завершении

# 9. Очистка после тестов
docker-compose down -v
```

## 🐳 Деплой с Docker Compose (простой вариант)

Для быстрого деплоя на любой машине с Docker:

```bash
# 1. Клонируйте репозиторий на сервер
git clone https://github.com/saymonuel52-cpu/vseinstrumenti-parser.git
cd vseinstrumenti-parser

# 2. Настройте переменные окружения
cp appsettings.Production.json appsettings.Local.json
# Отредактируйте appsettings.Local.json (Redis пароль, API ключи и т.д.)

# 3. Запустите приложение
docker-compose -f docker-compose.prod.yml up -d

# 4. Настройте reverse proxy (nginx)
sudo apt install nginx
sudo cp nginx.conf /etc/nginx/sites-available/vseinstrumenti-parser
sudo ln -s /etc/nginx/sites-available/vseinstrumenti-parser /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

## 🔐 Безопасность при деплое

### Обязательные шаги:
1. **Измените все пароли по умолчанию** в `appsettings.Production.json`
2. **Настройте firewall**:
   ```bash
   # Разрешите только необходимые порты
   sudo ufw allow 80/tcp    # HTTP
   sudo ufw allow 443/tcp   # HTTPS
   sudo ufw allow 22/tcp    # SSH
   sudo ufw enable
   ```
3. **Настройте HTTPS** с Let's Encrypt:
   ```bash
   sudo apt install certbot python3-certbot-nginx
   sudo certbot --nginx -d your-domain.com
   ```
4. **Включите аутентификацию** если API публичный:
   ```json
   {
     "Security": {
       "EnableAuthentication": true,
       "ApiKey": "your-strong-api-key-here"
     }
   }
   ```

## 📊 Мониторинг после деплоя

После деплоя убедитесь, что мониторинг работает:

1. **Prometheus targets**: http://your-prometheus:9090/targets
   - Должен видеть `vseinstrumenti-parser:8080` со статусом UP

2. **Grafana дашборд**: http://your-grafana:3000
   - Импортируйте дашборд из `grafana/dashboards/vseinstrumenti-parser-dashboard.json`

3. **Логи в Seq**: http://your-seq:5341
   - Проверьте, что логи поступают

4. **Настройте алерты**:
   - В Grafana создайте алерты на ключевые метрики
   - Или настройте Alertmanager в Prometheus

## 🚨 Аварийное восстановление

### Если деплой прошел неудачно:

1. **Откат к предыдущей версии**:
   ```bash
   # Для Docker Compose
   docker-compose pull
   docker-compose up -d
   
   # Для Kubernetes
   kubectl rollout undo deployment/vseinstrumenti-parser
   ```

2. **Проверка логов**:
   ```bash
   docker-compose logs vseinstrumenti-parser --tail=100
   docker-compose logs redis --tail=50
   ```

3. **Ручной запуск**:
   ```bash
   # Остановите проблемный контейнер
   docker-compose stop vseinstrumenti-parser
   
   # Запустите с подробным логированием
   docker-compose run --rm vseinstrumenti-parser \
     dotnet VseinstrumentiParser.dll --verbose
   ```

## 📈 Производительность после деплоя

После деплоя проведите нагрузочное тестирование:

```bash
# Установите bombardier
docker pull alpine/bombardier

# Тестирование health endpoint
docker run --rm alpine/bombardier \
  -c 100 -d 30s -l http://your-domain.com/health

# Тестирование API
docker run --rm alpine/bombardier \
  -c 50 -d 60s -l http://your-domain.com/api/categories
```

**Ожидаемые метрики:**
- Response time < 100ms для health checks
- Response time < 2s для API endpoints
- Error rate < 0.1%
- Memory usage < 512MB
- CPU usage < 30% под нагрузкой

## 🔄 Непрерывный мониторинг

Настройте регулярные проверки:

1. **Ежедневные health checks**:
   ```bash
   # Добавьте в crontab
   0 */6 * * * curl -f https://your-domain.com/health || \
     echo "Health check failed" | mail -s "Alert" admin@example.com
   ```

2. **Мониторинг дискового пространства**:
   ```bash
   # Проверка логов
   du -sh logs/
   
   # Очистка старых логов
   find logs/ -name "*.log" -mtime +30 -delete
   ```

3. **Обновление зависимостей**:
   ```bash
   # Еженедельное обновление Docker образов
   docker-compose pull
   docker-compose up -d
   ```

## 📞 Поддержка

Если возникли проблемы с деплоем:

1. **Проверьте логи GitHub Actions**
2. **Изучите логи приложения**: `docker-compose logs vseinstrumenti-parser`
3. **Проверьте конфигурацию**: убедитесь, что все секреты установлены правильно
4. **Создайте issue** в репозитории GitHub

Проект готов к production деплою! Удачи! 🚀