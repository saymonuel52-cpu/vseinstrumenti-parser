# Инструкция по загрузке проекта на GitHub

## 1. Создание репозитория на GitHub

1. Перейдите на [GitHub](https://github.com) и войдите в свой аккаунт
2. Нажмите кнопку "+" в правом верхнем углу и выберите "New repository"
3. Заполните информацию:
   - **Repository name**: `vseinstrumenti-parser`
   - **Description**: `Production-ready web parser for vseinstrumenti.ru and 220-volt.ru with monitoring, health checks, and Docker support`
   - Выберите "Public" или "Private"
   - **Не добавляйте** README, .gitignore или license (у нас уже есть эти файлы)
4. Нажмите "Create repository"

## 2. Добавление удаленного репозитория

Выполните следующие команды в терминале в папке проекта:

```bash
# Добавьте удаленный репозиторий (замените YOUR_USERNAME на ваш GitHub username)
git remote add origin https://github.com/YOUR_USERNAME/vseinstrumenti-parser.git

# Или если используете SSH:
# git remote add origin git@github.com:YOUR_USERNAME/vseinstrumenti-parser.git
```

## 3. Отправка кода на GitHub

```bash
# Отправьте код в ветку main
git push -u origin main

# Если возникнет ошибка из-за разницы в именах веток (master vs main):
git branch -M main
git push -u origin main
```

## 4. Проверка

После успешной отправки:
1. Перейдите на страницу вашего репозитория: `https://github.com/YOUR_USERNAME/vseinstrumenti-parser`
2. Убедитесь, что все файлы загружены
3. Проверьте, что README.md отображается корректно

## 5. Настройка GitHub Actions (опционально)

CI/CD пайплайн уже настроен в файле `.github/workflows/ci-cd.yml`. Для его активации:

1. Перейдите в репозитории на вкладку "Actions"
2. Включите workflows если они не активированы автоматически
3. Для работы тестов может потребоваться добавить секреты (secrets) в настройках репозитория:
   - `DOCKERHUB_USERNAME` - имя пользователя Docker Hub
   - `DOCKERHUB_TOKEN` - токен доступа Docker Hub

## 6. Настройка GitHub Pages для документации (опционально)

Для публикации документации API:

1. Перейдите в Settings → Pages
2. В разделе "Source" выберите "GitHub Actions"
3. Документация будет автоматически генерироваться при каждом коммите

## 7. Быстрый старт после загрузки

После загрузки на GitHub, проект можно быстро развернуть:

```bash
# Клонирование репозитория
git clone https://github.com/YOUR_USERNAME/vseinstrumenti-parser.git
cd vseinstrumenti-parser

# Запуск полного стека с Docker Compose
docker-compose up -d

# Или сборка и запуск только приложения
dotnet build
dotnet run --project VseinstrumentiParser.csproj
```

## 8. Структура репозитория

```
vseinstrumenti-parser/
├── .github/workflows/ci-cd.yml     # CI/CD пайплайн GitHub Actions
├── .gitignore                      # Игнорируемые файлы
├── .gitlab-ci.yml                  # CI/CD пайплайн GitLab CI
├── API_DOCUMENTATION.md            # Документация API
├── Dockerfile                      # Docker образ приложения
├── README.md                       # Основная документация
├── SECURITY_CONFIGURATION.md       # Руководство по безопасности
├── appsettings.json                # Базовая конфигурация
├── appsettings.Development.json    # Конфигурация для разработки
├── appsettings.Production.json     # Конфигурация для production
├── docker-compose.yml              # Полный стек Docker
├── grafana/                        # Дашборды и конфигурация Grafana
├── otel-collector-config.yaml      # Конфигурация OpenTelemetry
├── prometheus.yml                  # Конфигурация Prometheus
├── src/                            # Исходный код приложения
└── tests/                          # Тесты
```

## 9. Полезные ссылки после загрузки

- **Документация API**: `https://github.com/YOUR_USERNAME/vseinstrumenti-parser/blob/main/API_DOCUMENTATION.md`
- **CI/CD статус**: `https://github.com/YOUR_USERNAME/vseinstrumenti-parser/actions`
- **Docker образ**: `https://hub.docker.com/r/YOUR_USERNAME/vseinstrumenti-parser` (после настройки CI/CD)

## 10. Устранение проблем

### Ошибка: "remote origin already exists"
```bash
git remote remove origin
git remote add origin https://github.com/YOUR_USERNAME/vseinstrumenti-parser.git
```

### Ошибка: "failed to push some refs"
```bash
git pull origin main --allow-unrelated-histories
git push -u origin main
```

### Ошибка аутентификации
Убедитесь, что у вас есть права на запись в репозиторий. Используйте Personal Access Token если требуется.

## 11. Дальнейшие шаги

1. **Настройте защиту веток** в настройках репозитория
2. **Добавьте collaborators** если работаете в команде
3. **Настройте issues и projects** для отслеживания задач
4. **Добавьте badges** в README.md для отображения статуса сборки
5. **Опубликуйте пакет NuGet** если планируете использовать как библиотеку

Проект готов к использованию в production! 🚀