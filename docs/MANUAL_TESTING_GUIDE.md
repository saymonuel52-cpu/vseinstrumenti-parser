# Manual Testing Guide for Parser Web UI

## 📋 Pre-requisites

### 1. Install .NET 8.0 SDK
```powershell
# Download from:
https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
# Expected: 8.0.x
```

### 2. Check Docker (optional)
```powershell
docker --version
docker-compose --version
```

---

## 🚀 Quick Start (Manual)

### Step 1: Navigate to project
```powershell
cd ParserWebUI
```

### Step 2: Restore packages
```powershell
dotnet restore
```
Expected output:
```
  Determining projects to restore...
  Restored ParserWebUI/ParserWebUI.csproj (in X ms)
```

### Step 3: Build
```powershell
dotnet build
```
Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 4: Run
```powershell
dotnet run
```
Expected output:
```
info: Microsoft.Hosting.Lifetime[0]
      Now listening on: http://localhost:5000
```

### Step 5: Open browser
Navigate to: **http://localhost:5000**

---

## 🧪 Test Checklist

### ✅ Test 1: Dashboard Page
- [ ] URL: http://localhost:5000
- [ ] Page loads without errors
- [ ] Health status shows "OK"
- [ ] Metrics cards displayed (Products, Cache, Errors)
- [ ] Navigation menu visible
- [ ] Dark theme toggle button present

### ✅ Test 2: Parse Page
- [ ] URL: http://localhost:5000/parse
- [ ] Source selection works (vseinstrumenti/volt/both)
- [ ] Category dropdown loads
- [ ] Max products slider works (1-1000)
- [ ] "Запустить" button enabled
- [ ] Progress bar visible after start
- [ ] Logs appear in real-time

### ✅ Test 3: Compare Page
- [ ] URL: http://localhost:5000/compare
- [ ] Search box works
- [ ] Filters (price, brand, in-stock) apply
- [ ] Results table displays
- [ ] Price difference calculated
- [ ] Cache status shown

### ✅ Test 4: Logs Page
- [ ] URL: http://localhost:5000/logs
- [ ] Log table displays
- [ ] Level filter works (Error/Warning/Info)
- [ ] Search box filters logs
- [ ] Summary cards show counts

### ✅ Test 5: Settings Page
- [ ] URL: http://localhost:5000/settings
- [ ] Cache statistics displayed
- [ ] Alert toggles work
- [ ] Parser selectors editable
- [ ] Export/Import buttons present
- [ ] System info shown

### ✅ Test 6: Theme Toggle
- [ ] Click 🌙/☀️ button in header
- [ ] Theme changes immediately
- [ ] Selection persists after refresh
- [ ] Check localStorage in DevTools:
  ```javascript
  localStorage.getItem('theme')
  // Should be: "light" or "dark"
  ```

### ✅ Test 7: API Authentication
```powershell
# Get API key from appsettings.json
$apiKey = (Get-Content appsettings.json | ConvertFrom-Json).ApiKey

# Test wrong key (should fail)
curl -H "X-API-Key: wrong-key" http://localhost:5000/health
# Expected: 401 Unauthorized

# Test correct key (should succeed)
curl -H "X-API-Key: $apiKey" http://localhost:5000/health
# Expected: 200 OK with JSON
```

### ✅ Test 8: Health Checks
```powershell
# Liveness
curl http://localhost:5000/health/live
# Expected: {"status":"Healthy"}

# Readiness
curl http://localhost:5000/health/ready
# Expected: {"status":"Healthy"}

# Full status
curl http://localhost:5000/health
# Expected: Detailed status JSON
```

### ✅ Test 9: API Endpoints
```powershell
# Metrics
curl -H "X-API-Key: $apiKey" http://localhost:5000/api/metrics
# Expected: JSON with metrics

# Logs (recent)
curl -H "X-API-Key: $apiKey" http://localhost:5000/api/logs/recent
# Expected: JSON with recent logs
```

### ✅ Test 10: Docker Compose (optional)
```powershell
# Build and run
docker-compose -f docker-compose.webui.yml up -d

# Check status
docker-compose -f docker-compose.webui.yml ps

# View logs
docker-compose -f docker-compose.webui.yml logs -f webui

# Stop
docker-compose -f docker-compose.webui.yml down
```

---

## 🔧 Troubleshooting

### Issue: dotnet command not found
**Solution:**
```powershell
# Install .NET 8.0 SDK
winget install Microsoft.DotNet.SDK.8

# Or download manually:
https://dotnet.microsoft.com/download/dotnet/8.0

# Restart terminal after installation
```

### Issue: Port 5000 already in use
**Solution:**
```powershell
# Check what's using port 5000
netstat -ano | findstr :5000

# Kill process or change port
$env:ASPNETCORE_URLS = "http://localhost:5001"
dotnet run
```

### Issue: Build errors
**Solution:**
```powershell
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Check for missing packages
dotnet list package
```

### Issue: API Key authentication fails
**Solution:**
```powershell
# Check appsettings.json
Get-Content appsettings.json | ConvertFrom-Json | Select-Object ApiKey

# Regenerate if needed
$newKey = [System.Guid]::NewGuid().ToString("N")
$config = Get-Content appsettings.json | ConvertFrom-Json
$config.ApiKey = $newKey
$config | ConvertTo-Json -Depth 100 | Set-Content appsettings.json
```

### Issue: SignalR not connecting
**Solution:**
1. Open browser DevTools → Network tab
2. Look for `/hubs/parse-progress` connection
3. Check for WebSocket errors
4. Try different browser (Chrome recommended)

### Issue: Dark theme not persisting
**Solution:**
```javascript
// In browser console
localStorage.getItem('theme')
// Should be: "light" or "dark"

// Clear and retry
localStorage.removeItem('theme')
// Refresh page
```

---

## 📊 Expected Screenshots

### Dashboard
```
┌─────────────────────────────────────────┐
│  🚀 Parser Web UI                       │
├─────────────────────────────────────────┤
│  [Дашборд] [Запуск] [Сравнение] [Логи] │
└─────────────────────────────────────────┘

┌──────────────┬──────────────┬──────────────┬──────────────┐
│ Статус: OK   │ Товаров: 5420│ Кэш хиты: 3k│ Ошибок: 12   │
└──────────────┴──────────────┴──────────────┴──────────────┘

Быстрые действия:
[▶ Запустить парсинг] [🗑 Очистить кэш] [🔄 Обновить]

Активные задачи: 0
```

### Parse Page
```
┌─────────────────────────────┬─────────────────────────────┐
│ Настройки парсинга          │ Прогресс                    │
│                             │                             │
│ Источник: [●vseinstrumenti] │ [████████░░░░] 67%          │
│                             │                             │
│ Категория: Дрели ▼          │ Найдено: 67 | Ошибок: 0    │
│                             │ Время: 00:02:15             │
│ ✓ Пропустить кэш            │                             │
│ Лимит: 500 ─────●───        │ Лог выполнения:             │
│                             │ [14:23:45] [Info] Начало... │
│ [▶ Запустить]               │ [14:23:46] [Info] Найдено.. │
└─────────────────────────────┴─────────────────────────────┘
```

---

## 🎯 Success Criteria

All tests must pass:
- ✅ Application builds without errors
- ✅ Application starts on http://localhost:5000
- ✅ All 5 pages load correctly
- ✅ API authentication works
- ✅ Health checks return 200
- ✅ Dark theme toggles and persists
- ✅ Navigation works between pages
- ✅ No console errors in browser

---

## 📞 Support

If tests fail:
1. Check .NET version: `dotnet --version` (must be 8.0.x)
2. Check logs: Look at console output when running
3. Check dependencies: `dotnet restore`
4. Review error messages carefully
5. Check this guide for troubleshooting

---

**Last updated**: 2024  
**Version**: 1.0.0
