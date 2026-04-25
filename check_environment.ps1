# Скрипт проверки окружения для парсера vseinstrumenti.ru

Write-Host "=== Проверка окружения для парсера vseinstrumenti.ru ===" -ForegroundColor Cyan
Write-Host ""

# Проверка .NET SDK
Write-Host "1. Проверка наличия .NET SDK..." -ForegroundColor Yellow
$dotnetPath = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetPath) {
    $version = dotnet --version
    Write-Host "   .NET SDK найден: $version" -ForegroundColor Green
} else {
    Write-Host "   .NET SDK не найден!" -ForegroundColor Red
    Write-Host "   Для работы проекта необходимо установить .NET 8.0 или выше." -ForegroundColor Yellow
    Write-Host "   Скачать можно с: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
}

Write-Host ""

# Проверка структуры проекта
Write-Host "2. Проверка структуры проекта..." -ForegroundColor Yellow
$requiredFiles = @(
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

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "   ✓ $file" -ForegroundColor Green
    } else {
        Write-Host "   ✗ $file (отсутствует)" -ForegroundColor Red
        $missingFiles += $file
    }
}

Write-Host ""

if ($missingFiles.Count -gt 0) {
    Write-Host "   Найдены отсутствующие файлы: $($missingFiles.Count)" -ForegroundColor Red
} else {
    Write-Host "   Все файлы присутствуют!" -ForegroundColor Green
}

Write-Host ""

# Проверка зависимостей
Write-Host "3. Проверка зависимостей..." -ForegroundColor Yellow
if (Test-Path "VseinstrumentiParser.csproj") {
    $projContent = Get-Content "VseinstrumentiParser.csproj" -Raw
    if ($projContent -match "AngleSharp") {
        Write-Host "   ✓ AngleSharp указан в проекте" -ForegroundColor Green
    } else {
        Write-Host "   ✗ AngleSharp не найден в проекте" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Файл проекта не найден" -ForegroundColor Red
}

Write-Host ""

# Рекомендации
Write-Host "=== Рекомендации ===" -ForegroundColor Cyan
if ($dotnetPath -and $missingFiles.Count -eq 0) {
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