# Скрипт проверки health check после деплоя
# Использование: .\check_health.ps1 -Url "http://localhost:8080"

param(
    [string]$Url = "http://localhost:8080"
)

Write-Host "Проверка health check после деплоя" -ForegroundColor Green
Write-Host "Целевой URL: $Url" -ForegroundColor Yellow

# Функция для выполнения HTTP-запроса
function Test-HealthEndpoint {
    param([string]$Endpoint, [string]$Name)
    
    try {
        $response = Invoke-WebRequest -Uri $Endpoint -Method Get -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] ${Name}: УСПЕХ (HTTP $($response.StatusCode))" -ForegroundColor Green
            $healthData = $response.Content | ConvertFrom-Json
            Write-Host "   Статус: $($healthData.status)"
            Write-Host "   Время ответа: $($healthData.totalDuration) мс"
            return $true
        } else {
            Write-Host "[ERROR] ${Name}: ОШИБКА (HTTP $($response.StatusCode))" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "[ERROR] ${Name}: ОШИБКА ($($_.Exception.Message))" -ForegroundColor Red
        return $false
    }
}

# Проверка основных эндпоинтов
$endpoints = @(
    @{Name="Overall Health"; Path="/health"},
    @{Name="Readiness"; Path="/health/ready"},
    @{Name="Liveness"; Path="/health/live"},
    @{Name="Metrics"; Path="/metrics"},
    @{Name="API Categories"; Path="/api/categories"}
)

$allSuccess = $true

foreach ($ep in $endpoints) {
    $fullUrl = "$Url$($ep.Path)"
    $success = Test-HealthEndpoint -Endpoint $fullUrl -Name $ep.Name
    if (-not $success) {
        $allSuccess = $false
    }
    Start-Sleep -Milliseconds 200
}

# Итог
Write-Host "`n" + ("="*50) -ForegroundColor Cyan
if ($allSuccess) {
    Write-Host "✅ ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ УСПЕШНО!" -ForegroundColor Green
    Write-Host "   Приложение готово к работе в production." -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ НЕКОТОРЫЕ ПРОВЕРКИ НЕ ПРОШЛИ!" -ForegroundColor Red
    Write-Host "   Проверьте логи приложения и настройки." -ForegroundColor Yellow
    exit 1
}