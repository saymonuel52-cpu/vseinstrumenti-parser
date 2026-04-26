@echo off
REM
REM start.bat - Запуск vseinstrumenti-parser стека
REM Windows CMD скрипт с автоматическим открытием веб-интерфейса
REM

SETLOCAL EnableDelayedExpansion

REM Аргументы
SET REBUILD=0
SET NO_BROWSER=0
SET TIMEOUT=120

REM Парсинг аргументов
:parse_args
IF "%~1"=="" GOTO args_done
IF /I "%~1"=="--rebuild" SET REBUILD=1
IF /I "%~1"=="-r" SET REBUILD=1
IF /I "%~1"=="--no-browser" SET NO_BROWSER=1
IF /I "%~1"=="-n" SET NO_BROWSER=1
IF /I "%~1"=="--timeout" SET TIMEOUT=%~2
SHIFT
SHIFT
GOTO parse_args

:args_done

REM Цвета (используем echo для цветов, так как CMD ограничен)
SET NL=^


REM Функции вывода
:print_header
ECHO.
ECHO ========================================
ECHO   %~1
ECHO ========================================
ECHO.
GOTO :EOF

:print_success
ECHO ✅ %~1
GOTO :EOF

:print_info
ECHO ℹ️   %~1
GOTO :EOF

:print_warning
ECHO ⚠️   %~1
GOTO :EOF

:print_error
ECHO ❌ %~1
GOTO :EOF

REM Проверка зависимостей
CALL :print_header "Проверка зависимостей"

REM Проверка Docker
CALL :print_info "Проверка Docker..."
docker --version >nul 2>&1
IF ERRORLEVEL 1 (
    CALL :print_error "Docker не найден в PATH"
    ECHO.
    ECHO Установите Docker Desktop с: https://www.docker.com/products/docker-desktop
    ECHO После установки перезапустите командную строку.
    PAUSE
    EXIT /B 1
)
FOR /F "tokens=* USEBACKQ" %%A IN (`docker --version`) DO SET DOCKER_VERSION=%%A
CALL :print_success "Docker установлен: %DOCKER_VERSION%"

REM Проверка docker-compose
CALL :print_info "Проверка Docker Compose..."
docker-compose --version >nul 2>&1
IF ERRORLEVEL 1 (
    CALL :print_error "Docker Compose не найден"
    ECHO.
    ECHO Docker Compose должен идти вместе с Docker Desktop
    PAUSE
    EXIT /B 1
)
FOR /F "tokens=* USEBACKQ" %%A IN (`docker-compose --version`) DO SET COMPOSE_VERSION=%%A
CALL :print_success "Docker Compose установлен: %COMPOSE_VERSION%"

REM Проверка .NET SDK (опционально)
CALL :print_info "Проверка .NET SDK..."
dotnet --version >nul 2>&1
IF ERRORLEVEL 1 (
    CALL :print_warning ".NET SDK не найден (необязательно, если используете только Docker)"
) ELSE (
    FOR /F "tokens=* USEBACKQ" %%A IN (`dotnet --version`) DO SET DOTNET_VERSION=%%A
    CALL :print_success ".NET SDK установлен: %DOTNET_VERSION%"
)

REM Остановка старых контейнеров
CALL :print_header "Остановка старых сервисов"

CALL :print_info "Очистка старых контейнеров..."
docker-compose down --remove-orphans --volumes >nul 2>&1
CALL :print_success "Старые контейнеры остановлены"

REM Пересборка образов (если указано)
IF %REBUILD%==1 (
    CALL :print_header "Пересборка образов"
    CALL :print_info "Сборка Docker образов..."
    docker-compose build --no-cache
    IF ERRORLEVEL 1 (
        CALL :print_error "Ошибка при сборке образов"
        PAUSE
        EXIT /B 1
    )
    CALL :print_success "Образы пересобраны"
)

REM Запуск стека
CALL :print_header "Запуск сервисов"

CALL :print_info "Запуск 8 сервисов (parser, webui, redis, prometheus, grafana, seq, otel, alertmanager)..."
docker-compose up -d
IF ERRORLEVEL 1 (
    CALL :print_error "Ошибка при запуске контейнеров"
    ECHO.
    ECHO Проверьте логи: docker-compose logs -f
    PAUSE
    EXIT /B 1
)
CALL :print_success "Контейнеры запущены в фоновом режиме"

REM Ожидание готовности
CALL :print_header "Ожидание готовности сервисов"

SET HEALTH_URL=http://localhost:8080/health/ready
SET MAX_ATTEMPTS=40
SET ATTEMPT=0
SET READY=0

CALL :print_info "Ожидание запуска Web UI (%TIMEOUT% секунд максимум)..."

:wait_loop
SET /A ATTEMPT+=1
SET /A REMAINING=%TIMEOUT% - (%ATTEMPT% * 3)

SET /P =%NL%⏳ Ожидание... (%REMAINING% сек осталось)   <nul

curl -s -f -o nul "%HEALTH_URL%" --max-time 5 >nul 2>&1
IF NOT ERRORLEVEL 1 (
    SET READY=1
    GOTO :wait_done
)

IF %ATTEMPT% GEQ %MAX_ATTEMPTS% (
    GOTO :wait_done
)

timeout /t 3 /nobreak >nul
GOTO :wait_loop

:wait_done
echo.

IF %READY%==1 (
    CALL :print_success "Все сервисы готовы к работе!"
) ELSE (
    CALL :print_error "Таймаут ожидания! Сервисы не запустились за %TIMEOUT% секунд"
    ECHO.
    ECHO Проверьте логи:
    ECHO   docker-compose logs -f parser
    ECHO   docker-compose logs -f webui
    ECHO.
    ECHO Возможные причины:
    ECHO   - Порт 8080 занят другим приложением
    ECHO   - Недостаточно ресурсов (RAM/CPU)
    ECHO   - Ошибка в конфигурации docker-compose.yml
    PAUSE
    EXIT /B 1
)

REM Открытие браузера
IF %NO_BROWSER%==0 (
    CALL :print_header "Открытие веб-интерфейса"
    
    SET WEB_URL=http://localhost:8082
    CALL :print_info "Открытие %WEB_URL% в браузере..."
    
    start "" "%WEB_URL%"
    CALL :print_success "Браузер открыт!"
)

REM Финальная информация
CALL :print_header "🎉 Готово! Проект запущен"

ECHO.
ECHO 📊 Доступные сервисы:
ECHO    ┌─────────────────────────────────────────────┐
ECHO    │ Web UI (Blazor)        http://localhost:8082 │
ECHO    │ API                    http://localhost:8080 │
ECHO    │ Health Check           http://localhost:8080/health │
ECHO    │ Prometheus Metrics     http://localhost:9090 │
ECHO    │ Grafana Dashboards     http://localhost:3000 │
ECHO    │ Seq Logs               http://localhost:5341 │
ECHO    └─────────────────────────────────────────────┘
ECHO.
ECHO 🔑 API Key (по умолчанию):
ECHO    Заголовок: X-API-Key: your-api-key-here
ECHO    Или измените в ParserWebUI/appsettings.json
ECHO.
ECHO 🛠️  Управление контейнерами:
ECHO    Остановить:  docker-compose down
ECHO    Перезапуск: docker-compose restart
ECHO    Логи:        docker-compose logs -f
ECHO    Статус:      docker-compose ps
ECHO.
ECHO 📚 Документация:
ECHO    README.md           Основное описание
ECHO    docs/ARCHITECTURE.md Архитектура v2.0
ECHO    docs/TESTING.md     Тестирование
ECHO    DEPLOYMENT_GUIDE.md Развёртывание
ECHO.
ECHO 💡 Советы:
ECHO    - Для пересборки образов: start.bat --rebuild
ECHO    - Без открытия браузера: start.bat --no-browser
ECHO    - Своевременный таймаут: start.bat --timeout 180
ECHO.

PAUSE
