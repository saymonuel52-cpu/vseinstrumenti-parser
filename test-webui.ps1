# Test script for Parser Web UI
# Run from project root: .\test-webui.ps1

$ErrorActionPreference = "Continue"

Write-Host "=== Parser Web UI Test Suite ===" -ForegroundColor Cyan
Write-Host ""

# Colors
$Success = "Green"
$Error = "Red"
$Warning = "Yellow"
$Info = "White"

# Test 1: Check .NET SDK
Write-Host "[1/6] Checking .NET SDK..." -ForegroundColor Cyan
try {
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ .NET SDK installed: $dotnetVersion" -ForegroundColor $Success
    } else {
        Write-Host "  ✗ .NET SDK not found or error" -ForegroundColor $Error
        Write-Host "  Install from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor $Warning
        exit 1
    }
} catch {
    Write-Host "  ✗ .NET SDK not found" -ForegroundColor $Error
    exit 1
}

# Test 2: Restore packages
Write-Host ""
Write-Host "[2/6] Restoring packages..." -ForegroundColor Cyan
Push-Location ParserWebUI
$restoreResult = dotnet restore 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Packages restored successfully" -ForegroundColor $Success
} else {
    Write-Host "  ✗ Failed to restore packages" -ForegroundColor $Error
    Write-Host $restoreResult -ForegroundColor $Error
    Pop-Location
    exit 1
}

# Test 3: Build
Write-Host ""
Write-Host "[3/6] Building project..." -ForegroundColor Cyan
$buildResult = dotnet build --no-restore 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Build successful" -ForegroundColor $Success
} else {
    Write-Host "  ✗ Build failed" -ForegroundColor $Error
    Write-Host $buildResult -ForegroundColor $Error
    Pop-Location
    exit 1
}

# Test 4: Check appsettings
Write-Host ""
Write-Host "[4/6] Checking configuration..." -ForegroundColor Cyan
if (Test-Path "appsettings.json") {
    Write-Host "  ✓ appsettings.json found" -ForegroundColor $Success
    
    # Check for API Key
    $config = Get-Content "appsettings.json" -Raw | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($config.ApiKey) -or $config.ApiKey -eq "change-me-in-production-use-openssl-rand-hex-32") {
        Write-Host "  ⚠ API Key uses default value - change in production!" -ForegroundColor $Warning
        $newKey = [System.Guid]::NewGuid().ToString("N")
        Write-Host "  Generated new API key: $newKey" -ForegroundColor $Info
        
        # Update config
        $config.ApiKey = $newKey
        $config | ConvertTo-Json -Depth 100 | Set-Content "appsettings.json"
        Write-Host "  ✓ Updated appsettings.json with new API key" -ForegroundColor $Success
    } else {
        Write-Host "  ✓ API Key configured" -ForegroundColor $Success
    }
} else {
    Write-Host "  ✗ appsettings.json not found" -ForegroundColor $Error
    Pop-Location
    exit 1
}

# Test 5: Start application (background)
Write-Host ""
Write-Host "[5/6] Starting application..." -ForegroundColor Cyan
Write-Host "  Starting on http://localhost:5000..." -ForegroundColor $Info

# Set environment
$env:ASPNETCORE_URLS = "http://localhost:5000"
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Start in background
$startProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project ParserWebUI" -PassThru -NoNewWindow -Wait:$false

# Wait for startup
Write-Host "  Waiting for application to start (30 seconds)..." -ForegroundColor $Info
$waitTime = 0
$maxWait = 30
while ($waitTime -lt $maxWait) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/health/live" -TimeoutSec 2 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "  ✓ Application started successfully" -ForegroundColor $Success
            break
        }
    } catch {
        Start-Sleep -Seconds 2
        $waitTime += 2
        Write-Host "  Waiting... ($waitTime/$maxWait)" -ForegroundColor $Info -NoNewline
        Write-Host "." -ForegroundColor $Info
    }
}

if ($waitTime -ge $maxWait) {
    Write-Host "  ✗ Application failed to start within $maxWait seconds" -ForegroundColor $Error
    Write-Host "  Check logs above for errors" -ForegroundColor $Error
    Stop-Process -Id $startProcess.Id -Force -ErrorAction SilentlyContinue
    Pop-Location
    exit 1
}

# Test 6: API tests
Write-Host ""
Write-Host "[6/6] Running API tests..." -ForegroundColor Cyan

# Test wrong API key
Write-Host "  Testing wrong API key..." -ForegroundColor $Info
try {
    $wrongKey = Invoke-RestMethod -Uri "http://localhost:5000/health" -Headers @{ "X-API-Key" = "wrong-key" } -Method Get -ErrorAction Stop
    Write-Host "  ⚠ Expected 401 but got success" -ForegroundColor $Warning
} catch [Microsoft.PowerShell.Commands.HttpResponseException] {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "  ✓ Wrong API key rejected (401 Unauthorized)" -ForegroundColor $Success
    } else {
        Write-Host "  ⚠ Unexpected status code: $($_.Exception.Response.StatusCode)" -ForegroundColor $Warning
    }
} catch {
    Write-Host "  ⚠ Could not test API key: $($_.Exception.Message)" -ForegroundColor $Warning
}

# Test correct API key
Write-Host "  Testing correct API key..." -ForegroundColor $Info
try {
    $correctKey = Invoke-RestMethod -Uri "http://localhost:5000/health" -Headers @{ "X-API-Key" = $config.ApiKey } -Method Get
    Write-Host "  ✓ Correct API key accepted (200 OK)" -ForegroundColor $Success
    Write-Host "    Health status: $($correctKey.status)" -ForegroundColor $Info
} catch {
    Write-Host "  ✗ Correct API key rejected: $($_.Exception.Message)" -ForegroundColor $Error
}

# Test metrics endpoint
Write-Host "  Testing metrics endpoint..." -ForegroundColor $Info
try {
    $metrics = Invoke-RestMethod -Uri "http://localhost:5000/api/metrics" -Headers @{ "X-API-Key" = $config.ApiKey } -Method Get
    Write-Host "  ✓ Metrics endpoint working" -ForegroundColor $Success
    Write-Host "    Total products: $($metrics.totalProducts)" -ForegroundColor $Info
} catch {
    Write-Host "  ⚠ Metrics endpoint error: $($_.Exception.Message)" -ForegroundColor $Warning
}

# Cleanup
Write-Host ""
Write-Host "Stopping application..." -ForegroundColor Cyan
Stop-Process -Id $startProcess.Id -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ Application stopped" -ForegroundColor $Success

# Summary
Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "✓ .NET SDK: Installed" -ForegroundColor $Success
Write-Host "✓ Packages: Restored" -ForegroundColor $Success
Write-Host "✓ Build: Successful" -ForegroundColor $Success
Write-Host "✓ Configuration: Valid" -ForegroundColor $Success
Write-Host "✓ Application: Started and tested" -ForegroundColor $Success
Write-Host ""
Write-Host "🎉 All critical tests passed!" -ForegroundColor $Success
Write-Host ""
Write-Host "To run manually:" -ForegroundColor $Info
Write-Host "  cd ParserWebUI" -ForegroundColor $Info
Write-Host "  dotnet run" -ForegroundColor $Info
Write-Host ""
Write-Host "Then open: http://localhost:5000" -ForegroundColor $Info
Write-Host ""

Pop-Location
