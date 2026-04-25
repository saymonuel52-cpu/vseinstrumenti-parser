# Безопасная конфигурация для Production

## 1. User Secrets (для разработки)

### Настройка User Secrets
```bash
# Инициализация User Secrets
dotnet user-secrets init

# Добавление секретов
dotnet user-secrets set "Redis:Password" "your_redis_password"
dotnet user-secrets set "Security:ApiKey" "your_api_key"
dotnet user-secrets set "ExternalServices:Vseinstrumenti:ApiKey" "vseinstrumenti_api_key"
dotnet user-secrets set "Monitoring:Alerting:SlackWebhookUrl" "https://hooks.slack.com/..."
dotnet user-secrets set "Monitoring:Alerting:TelegramBotToken" "your_telegram_bot_token"
```

### Список рекомендуемых секретов
```
Redis:Password
Security:ApiKey
ExternalServices:Vseinstrumenti:ApiKey
ExternalServices:Volt220:ApiKey
Monitoring:Alerting:SlackWebhookUrl
Monitoring:Alerting:TelegramBotToken
Monitoring:Alerting:EmailSmtpServer
Monitoring:Alerting:EmailPassword
ExportSettings:EncryptionKey
```

## 2. Azure Key Vault (для Production)

### Настройка Azure Key Vault
1. Создайте Key Vault в Azure Portal
2. Добавьте секреты через портал или CLI:
```bash
az keyvault secret set --vault-name "vseinstrumenti-vault" --name "Redis--Password" --value "your_password"
az keyvault secret set --vault-name "vseinstrumenti-vault" --name "Security--ApiKey" --value "your_api_key"
```

### Интеграция с приложением
Добавьте пакеты:
```xml
<PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.3.0" />
<PackageReference Include="Azure.Identity" Version="1.10.4" />
```

Обновите `Program.cs`:
```csharp
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    var keyVaultUrl = builder.Configuration["KeyVault:Url"];
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential());
}
```

## 3. Environment Variables (для Docker/Kubernetes)

### Основные переменные окружения
```bash
# Redis
REDIS__CONNECTIONSTRING=redis:6379,password=${REDIS_PASSWORD}
REDIS_PASSWORD=your_password

# Security
SECURITY__APIKEY=your_api_key
SECURITY__ENABLEAUTHENTICATION=true

# Monitoring
MONITORING__ALERTING__SLACKWEBHOOKURL=https://hooks.slack.com/...
MONITORING__ALERTING__TELEGRAMBOTTOKEN=your_token

# External Services
EXTERNALSERVICES__VSEINSTRUMENTI__APIKEY=vseinstrumenti_key
EXTERNALSERVICES__VOLT220__APIKEY=220volt_key
```

### Docker Compose пример
```yaml
environment:
  - REDIS_PASSWORD=${REDIS_PASSWORD}
  - SECURITY_APIKEY=${SECURITY_APIKEY}
  - ASPNETCORE_ENVIRONMENT=Production
```

## 4. Конфигурационные файлы по средам

### Иерархия загрузки конфигурации
1. `appsettings.json` (базовые настройки)
2. `appsettings.{Environment}.json` (настройки окружения)
3. User Secrets (только Development)
4. Environment Variables
5. Azure Key Vault (Production)
6. Командная строка

### Рекомендуемая структура
```
/app
├── appsettings.json              # Базовые настройки
├── appsettings.Development.json  # Настройки разработки
├── appsettings.Staging.json      # Настройки staging
├── appsettings.Production.json   # Настройки production
└── secrets.json                  # User Secrets (не в репозитории)
```

## 5. Безопасные значения по умолчанию

### В `appsettings.Production.json` оставьте пустые значения:
```json
{
  "Redis": {
    "ConnectionString": "redis:6379,password=,abortConnect=false"
  },
  "Security": {
    "ApiKey": ""
  },
  "Monitoring": {
    "Alerting": {
      "SlackWebhookUrl": "",
      "TelegramBotToken": ""
    }
  }
}
```

### Заполнение через переменные окружения:
```bash
export Redis__Password="$(cat /run/secrets/redis-password)"
export Security__ApiKey="$(cat /run/secrets/api-key)"
```

## 6. Docker Secrets (для Swarm/Kubernetes)

### Создание secrets
```bash
echo "your_redis_password" | docker secret create redis_password -
echo "your_api_key" | docker secret create api_key -
```

### Использование в docker-compose.yml
```yaml
secrets:
  redis_password:
    external: true
  api_key:
    external: true

services:
  vseinstrumenti-parser:
    image: vseinstrumenti-parser:latest
    secrets:
      - redis_password
      - api_key
    environment:
      - REDIS__PASSWORD_FILE=/run/secrets/redis_password
      - SECURITY__APIKEY_FILE=/run/secrets/api_key
```

## 7. Ротация секретов

### Автоматическая ротация
1. **Redis пароли**: Каждые 90 дней
2. **API ключи**: Каждые 180 дней
3. **SSL сертификаты**: Каждые 365 дней

### Процесс ротации
```bash
# 1. Создать новый секрет
az keyvault secret set --vault-name "vseinstrumenti-vault" --name "Redis--Password-v2" --value "new_password"

# 2. Обновить приложение
export REDIS__PASSWORD="new_password"

# 3. Удалить старый секрет (после подтверждения работы)
az keyvault secret delete --vault-name "vseinstrumenti-vault" --name "Redis--Password"
```

## 8. Аудит и мониторинг

### Логирование доступа к секретам
```csharp
public class SecretAccessLogger
{
    private readonly ILogger<SecretAccessLogger> _logger;
    
    public void LogSecretAccess(string secretName, bool success)
    {
        _logger.LogInformation("Secret access: {SecretName}, Success: {Success}, User: {User}, Time: {Time}",
            secretName, success, Environment.UserName, DateTime.UtcNow);
    }
}
```

### Azure Key Vault аудит
- Включите диагностику Key Vault
- Настройте оповещения на подозрительные операции
- Регулярно проверяйте логи доступа

## 9. Экстренные процедуры

### Утечка секрета
1. Немедленно отозвать/заблокировать секрет
2. Сгенерировать новый секрет
3. Обновить все системы
4. Провести аудит логов
5. Уведомить заинтересованные стороны

### Резервное копирование секретов
```bash
# Экспорт всех секретов из Key Vault
az keyvault secret list --vault-name "vseinstrumenti-vault" --query "[].name" -o tsv | \
xargs -I {} az keyvault secret backup --vault-name "vseinstrumenti-vault" --name {} --file {}.backup
```

## 10. Контрольный список безопасности

- [ ] Все пароли и ключи удалены из `appsettings.Production.json`
- [ ] Используется Azure Key Vault или аналогичный менеджер секретов
- [ ] Настроена аутентификация для доступа к секретам
- [ ] Включено логирование доступа к секретам
- [ ] Регулярная ротация секретов
- [ ] Резервное копирование секретов
- [ ] Ограниченный доступ к секретам (принцип наименьших привилегий)
- [ ] Шифрование секретов в rest и transit
- [ ] Периодический аудит доступа
- [ ] План действий при утечке секретов