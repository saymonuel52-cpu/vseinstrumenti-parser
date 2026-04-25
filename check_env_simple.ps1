# Простой скрипт проверки окружения

Write-Host "=== Проверка окружения для парсера vseinstrumenti.ru ===" -ForegroundColor Cyan
Write-Host ""

# Проверка .NET SDK
Write-Host "1. Проверка наличия .NET SDK..." -ForegroundColor Yellow
try {
    $version = dotnet --version
    Write-Host "   .NET SDK найден: $version" -ForegroundColor Green
} catch {
    Write-Host "   .NET SDK не найден!" -ForegroundColor Red
    Write-Host "   Для работы проекта необходимо установить .NET 8.0 или выше." -ForegroundColor Yellow
    Write-Host "   Скачать можно с: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
}

Write-Host ""

# Проверка структуры проекта
Write-Host "2. Проверка структуры проекта..." -ForegroundColor Yellow
$files = @(
    "Program.cs",
    "VseinstrumentiParser.csproj",
    "Models/Category.cs",
    "Models/Product.cs",
    "Interfaces/ICategoryParser.cs",
    "Interfaces/IProductParser.cs",
    "Services/HttpClientService.cs",
    "Services/CategoryParser.cs",
    "Services/ProductParser.cs",
    "Services/VseinstrumentiParserService.cs",
    "Utilities/RetryPolicy.cs"
)

$missing = 0
foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "   ✓ $file" -ForegroundColor Green
    } else {
        Write-Host "   ✗ $file (отсутствует)" -ForegroundColor Red
        $missing++
    }
}

Write-Host ""

if ($missing -gt 0) {
    Write-Host "   Найдены отсутствующие файлы: $missing" -ForegroundColor Red
} else {
    Write-Host "   Все файлы присутствуют!" -ForegroundColor Green
}

Write-Host ""

# Рекомендации
Write-Host "=== Рекомендации ===" -ForegroundColor Cyan
if ($version -and $missing -eq 0) {
    Write-Host "1. Восстановите зависимости: dotnet restore" -ForegroundColor White
    Write-Host "2. Соберите проект: dotnet build" -ForegroundColor White
    Write-Host "3. Запустите парсер: dotnet run" -ForegroundColor White
    Write-Host "4. Для экспорта в CSV: dotnet run > vseinstrumenti_export.csv" -ForegroundColor White
} else {
    Write-Host "1. Установите .NET SDK с https://dotnet.microsoft.com" -ForegroundColor White
    Write-Host "2. Убедитесь, что все файлы проекта на месте" -ForegroundColor White
    Write-Host "3. После установки .NET выполните команды выше" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Проверка завершена ===" -ForegroundColor Cyan