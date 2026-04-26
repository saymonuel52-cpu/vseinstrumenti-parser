#!/usr/bin/env pwsh
#
# start.ps1 - Запуск vseinstrumenti-parser стека
# Windows PowerShell скрипт с автоматическим открытием веб-интерфейса
#

param(
    [switch]$Rebuild,
    [switch]$NoBrowser,
    [int]$Timeout = 120
)

$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "Vseinstrumenti Parser - Запуск..."

# Цвета для вывода
$Colors = @{
    Success = "Green"
    Info = "Cyan"
    Warning = "Yellow"
    Error = "Red"
    Header = "Blue"
}

# Функция для красивого вывода
function Write-Header {
    param([string]$Text)
    Write-Host "`n========================================" -ForegroundColor $Colors.Header
    Write-Host "  $Text" -ForegroundColor $Colors.Header
    Write-Host "========================================`n" -ForegroundColor $Colors.Header
}

function Write-Success {
    param([string]$Text)
    Write-Host "✅ $Text" -ForegroundColor $Colors.Success
}

function Write-Info {
    param([string]$Text)
    Write-Host "ℹ️  $Text" -ForegroundColor $Colors.Info
}

function Write-Warning-Message {
    param([string]$Text)
    Write-Host "⚠️  $Text" -ForegroundColor $Colors.Warning
}

function Write-Error-Message {
    param([string]$Text)
    Write-Host "❌ $Text" -ForegroundColor $Colors.Error
}

# Проверка зависимостей
Write-Header "Проверка зависимостей"

# Проверка Docker
Write-Info "Проверка Docker..."
try {
    $dockerVersion = docker --version 2>&1
    Write-Success "Docker установлен: $dockerVersion"
} catch {
    Write-Error-Message "Docker не найден в PATH"
    Write-Host "`nУстановите Docker Desktop с: https://www.docker.com/products/docker-desktop"
    Write-Host "После установки перезапустите терминал.`n"
    exit 1
}

# Проверка docker-compose
Write-Info "Проверка Docker Compose..."
try {
    $composeVersion = docker-compose --version 2>&1
    Write-Success "Docker Compose установлен: $composeVersion"
} catch {
    Write-Error-Message "Docker Compose не найден"
    Write-Host "`nDocker Compose должен идти вместе с Docker Desktop`n"
    exit 1
}

# Проверка .NET SDK (для локального запуска, опционально)
Write-Info "Проверка .NET SDK..."
try {
    $dotnetVersion = dotnet --version 2>&1
    Write-Success ".NET SDK установлен: $dotnetVersion"
} catch {
    Write-Warning-Message ".NET SDK не найден (необязательно, если используете только Docker)"
}

# Остановка старых контейнеров
Write-Header "Остановка старых сервисов"

Write-Info "Очистка старых контейнеров..."
try {
    docker-compose down --remove-orphans --volumes 2>$null
    Write-Success "Старые контейнеры остановлены"
} catch {
    Write-Warning-Message "Не удалось остановить контейнеры (возможно, они не были запущены)"
}

# Пересборка образов (если указано)
if ($Rebuild) {
    Write-Header "Пересборка образов"
    Write-Info "Сборка Docker образов..."
    try {
        docker-compose build --no-cache
        Write-Success "Образы пересобраны"
    } catch {
        Write-Error-Message "Ошибка при сборке образов"
        exit 1
    }
}

# Запуск стека
Write-Header "Запуск сервисов"

Write-Info "Запуск 8 сервисов (parser, webui, redis, prometheus, grafana, seq, otel, alertmanager)..."
try {
    docker-compose up -d
    Write-Success "Контейнеры запущены в фоновом режиме"
} catch {
    Write-Error-Message "Ошибка при запуске контейнеров"
    Write-Host "`nПроверьте логи: docker-compose logs -f`n"
    exit 1
}

# Ожидание готовности
Write-Header "Ожидание готовности сервисов"

$healthUrl = "http://localhost:8080/health/ready"
$maxAttempts = [math]::Floor($Timeout / 3)
$attempt = 0
$ready = $false

Write-Info "Ожидание запуска Web UI ($Timeout секунд максимум)..."

while ($attempt -lt $maxAttempts) {
    $attempt++
    $remaining = $Timeout - ($attempt * 3)
    
    Write-Host "`r⏳ Ожидание... ($remaining сек осталось)   " -NoNewline -ForegroundColor $Colors.Info
    
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    } catch {
        # Сервис ещё не готов, ждём
    }
    
    Start-Sleep -Seconds 3
}

Write-Host "`n" -NoNewline

if ($ready) {
    Write-Success "Все сервисы готовы к работе!"
} else {
    Write-Error-Message "Таймаут ожидания! Сервисы не запустились за $Timeout секунд"
    Write-Host "`nПроверьте логи:"
    Write-Host "  docker-compose logs -f parser"
    Write-Host "  docker-compose logs -f webui"
    Write-Host "`nВозможные причины:"
    Write-Host "  - Порт 8080 занят другим приложением"
    Write-Host "  - Недостаточно ресурсов (RAM/CPU)"
    Write-Host "  - Ошибка в конфигурации docker-compose.yml`n"
    exit 1
}

# Открытие браузера
if (-not $NoBrowser) {
    Write-Header "Открытие веб-интерфейса"
    
    $webUrl = "http://localhost:8082"
    Write-Info "Открытие $webUrl в браузере..."
    
    try {
        Start-Process $webUrl
        Write-Success "Браузер открыт!"
    } catch {
        Write-Warning-Message "Не удалось автоматически открыть браузер"
        Write-Host "`nОткройте вручную: $webUrl`n"
    }
}

# Финальная информация
Write-Header "🎉 Готово! Проект запущен"

Write-Host @"

📊 Доступные сервисы:
   ┌─────────────────────────────────────────────┐
   │ Web UI (Blazor)        http://localhost:8082 │
   │ API                    http://localhost:8080 │
   │ Health Check           http://localhost:8080/health │
   │ Prometheus Metrics     http://localhost:9090 │
   │ Grafana Dashboards     http://localhost:3000 │
   │ Seq Logs               http://localhost:5341 │
   └─────────────────────────────────────────────┘

🔑 API Key (по умолчанию):
   Заголовок: X-API-Key: your-api-key-here
   Или измените в ParserWebUI/appsettings.json

🛠️  Управление контейнерами:
   Остановить:  docker-compose down
   Перезапуск: docker-compose restart
   Логи:        docker-compose logs -f
   Статус:      docker-compose ps

📚 Документация:
   README.md           Основное описание
   docs/ARCHITECTURE.md Архитектура v2.0
   docs/TESTING.md     Тестирование
   DEPLOYMENT_GUIDE.md Развёртывание

💡 Советы:
   - Для пересборки образов: .\start.ps1 -Rebuild
   - Без открытия браузера: .\start.ps1 -NoBrowser
   - Своевременный таймаут: .\start.ps1 -Timeout 180

"@

Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
