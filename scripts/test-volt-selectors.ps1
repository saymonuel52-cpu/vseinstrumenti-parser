# Script to test 220-volt.ru selectors
# Gets HTML from real site and saves for analysis

$ErrorActionPreference = "Stop"

# Load required assemblies
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Web

# URLs for testing
$categoryUrl = "https://www.220-volt.ru/catalog-9889-elektroinstrumenty/"
$productUrl = "https://www.220-volt.ru/catalog-10125-dreti/"

$basePath = "test-data"
if (-not (Test-Path $basePath)) {
    New-Item -ItemType Directory -Path $basePath | Out-Null
}

Write-Host "=== Testing 220-volt.ru selectors ===" -ForegroundColor Cyan
Write-Host ""

# Create HttpClient with browser headers
$httpClient = New-Object System.Net.Http.HttpClient
$httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
$httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
$httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7")
$httpClient.Timeout = [System.TimeSpan]::FromSeconds(30)

try {
    # Get category HTML
    Write-Host "Getting category HTML: $categoryUrl" -ForegroundColor Yellow
    $categoryResponse = $httpClient.GetAsync($categoryUrl).Result
    $categoryResponse.EnsureSuccessStatusCode()
    $categoryContent = $categoryResponse.Content.ReadAsStringAsync().Result
    
    $categoryPath = Join-Path $basePath "volt-category.html"
    [System.IO.File]::WriteAllText($categoryPath, $categoryContent, [System.Text.Encoding]::UTF8)
    Write-Host "[OK] Saved to: $categoryPath" -ForegroundColor Green
    Write-Host "  Size: $($categoryContent.Length) bytes" -ForegroundColor Gray
    Write-Host ""
    
    # Get product HTML
    Write-Host "Getting product HTML: $productUrl" -ForegroundColor Yellow
    $productResponse = $httpClient.GetAsync($productUrl).Result
    $productResponse.EnsureSuccessStatusCode()
    $productContent = $productResponse.Content.ReadAsStringAsync().Result
    
    $productPath = Join-Path $basePath "volt-product.html"
    [System.IO.File]::WriteAllText($productPath, $productContent, [System.Text.Encoding]::UTF8)
    Write-Host "[OK] Saved to: $productPath" -ForegroundColor Green
    Write-Host "  Size: $($productContent.Length) bytes" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "=== Done! ===" -ForegroundColor Cyan
    Write-Host "Open saved HTML files in browser or code editor for analysis." -ForegroundColor Yellow
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
} finally {
    $httpClient.Dispose()
}
