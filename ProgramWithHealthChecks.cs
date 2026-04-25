using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using VseinstrumentiParser.Services;
using VseinstrumentiParser.Services.Health;
using VseinstrumentiParser.Services.Caching;
using VseinstrumentiParser.Services.Http;
using VseinstrumentiParser.Services.Telemetry;
using VseinstrumentiParser.Services.Alerting;
using VseinstrumentiParser.Utilities;
using Microsoft.OpenApi.Models;

namespace VseinstrumentiParser
{
    public class ProgramWithHealthChecks
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Конфигурация
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            
            // Логирование
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.AddSerilogLogger();
            
            // Сервисы
            ConfigureServices(builder.Services, builder.Configuration);
            
            // Health Checks
            ConfigureHealthChecks(builder.Services, builder.Configuration);
            
            var app = builder.Build();
            
            // Middleware
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseRouting();
            
            // Swagger/OpenAPI
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Vseinstrumenti Parser API v1");
                    options.RoutePrefix = "api-docs";
                    options.DocumentTitle = "Vseinstrumenti Parser API Documentation";
                    options.DefaultModelsExpandDepth(-1); // Скрыть схемы моделей по умолчанию
                });
            }
            
            // Health Checks эндпоинты
            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = WriteHealthCheckResponse
            });
            
            app.UseHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = WriteHealthCheckResponse
            });
            
            app.UseHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live"),
                ResponseWriter = WriteHealthCheckResponse
            });
            
            // Метрики для Prometheus
            app.MapGet("/metrics", async context =>
            {
                context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                await context.Response.WriteAsync("# Метрики парсера vseinstrumenti.ru\n");
                await context.Response.WriteAsync($"# Время: {DateTime.UtcNow:O}\n");
                await context.Response.WriteAsync("parser_uptime_seconds " + 
                    (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds + "\n");
            });
            
            // Основной эндпоинт
            app.MapGet("/", async context =>
            {
                await context.Response.WriteAsync(@"
<!DOCTYPE html>
<html>
<head>
    <title>Vseinstrumenti Parser API</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        h1 { color: #333; }
        .endpoint { background: #f5f5f5; padding: 10px; margin: 10px 0; border-left: 4px solid #007bff; }
    </style>
</head>
<body>
    <h1>Vseinstrumenti Parser API</h1>
    <p>Парсер каталога электроинструментов vseinstrumenti.ru и 220-volt.ru</p>
    
    <div class='endpoint'>
        <strong>GET /health</strong> - Полная проверка здоровья приложения
    </div>
    <div class='endpoint'>
        <strong>GET /health/ready</strong> - Проверка готовности (Redis, внешние сайты)
    </div>
    <div class='endpoint'>
        <strong>GET /health/live</strong> - Проверка живости (базовая)
    </div>
    <div class='endpoint'>
        <strong>GET /metrics</strong> - Метрики в формате Prometheus
    </div>
    <div class='endpoint'>
        <strong>GET /api/categories</strong> - Получить список категорий
    </div>
    <div class='endpoint'>
        <strong>GET /api/products/{categoryId}</strong> - Получить товары по категории
    </div>
    
    <p>Версия: 1.0.0 | .NET 8.0 | Режим: " + builder.Environment.EnvironmentName + @"</p>
</body>
</html>");
            });
            
            // API эндпоинты
            app.MapGet("/api/categories", async (VseinstrumentiParserService parserService) =>
            {
                var categories = await parserService.GetCategoriesAsync();
                return Results.Ok(categories);
            });
            
            app.MapGet("/api/products/{categoryUrl}", async (string categoryUrl, VseinstrumentiParserService parserService) =>
            {
                var products = await parserService.GetProductsFromCategoryAsync(categoryUrl, maxPages: 2);
                return Results.Ok(products);
            });
            
            // Graceful Shutdown
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() =>
            {
                var logger = app.Services.GetRequiredService<ILogger<ProgramWithHealthChecks>>();
                logger.LogInformation("=== Начало Graceful Shutdown ===");
                
                try
                {
                    // 1. Остановка приема новых HTTP-запросов
                    logger.LogInformation("1. Прекращение приема новых запросов...");
                    
                    // 2. Закрытие Redis соединения
                    var redis = app.Services.GetService<IConnectionMultiplexer>();
                    if (redis != null)
                    {
                        logger.LogInformation("2. Закрытие Redis соединения...");
                        redis.Close();
                        redis.Dispose();
                    }
                    
                    // 3. Остановка HTTP-клиентов
                    logger.LogInformation("3. Остановка HTTP-клиентов...");
                    var httpClientFactory = app.Services.GetService<IHttpClientFactory>();
                    // HttpClientFactory управляет жизненным циклом автоматически
                    
                    // 4. Сохранение состояния кэша (если нужно)
                    logger.LogInformation("4. Сохранение состояния кэша...");
                    var cacheService = app.Services.GetService<DistributedCacheService>();
                    cacheService?.Flush();
                    
                    // 5. Закрытие логгеров
                    logger.LogInformation("5. Закрытие логгеров...");
                    
                    logger.LogInformation("=== Graceful Shutdown завершен ===");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка во время Graceful Shutdown");
                }
            });
            
            // Запуск
            var logger = app.Services.GetRequiredService<ILogger<ProgramWithHealthChecks>>();
            logger.LogInformation("Запуск Vseinstrumenti Parser на порту 8080");
            logger.LogInformation("Health Checks доступны по адресу: http://localhost:8080/health");
            
            await app.RunAsync("http://*:8080");
        }
        
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Swagger/OpenAPI
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Vseinstrumenti Parser API",
                    Version = "v1.0.0",
                    Description = "API для парсинга каталога электроинструментов vseinstrumenti.ru и 220-volt.ru",
                    Contact = new OpenApiContact
                    {
                        Name = "Development Team",
                        Email = "dev@example.com"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });
                
                // Включение аннотаций
                options.EnableAnnotations();
                
                // Добавление XML комментариев
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
                
                // Настройка безопасности (если нужно)
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
            });
            
            // Основные сервисы
            services.AddSingleton<VseinstrumentiParserService>();
            services.AddSingleton<HttpClientService>();
            services.AddSingleton<CategoryParser>();
            services.AddSingleton<ProductParser>();
            
            // Фоновые задачи с Graceful Shutdown
            services.AddHostedService<BackgroundTaskService>();
            
            // Кэширование
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
                options.InstanceName = "VseinstrumentiParser";
            });
            
            services.AddSingleton<DistributedCacheService>();
            services.AddSingleton<CacheService>();
            
            // HTTP-клиенты
            services.AddHttpClient("HealthCheck")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseCookies = false
                });
            
            services.AddSingleton<ResilientHttpClientService>();
            
            // Телеметрия
            services.AddSingleton<OpenTelemetryService>();
            
            // Логирование
            services.AddSingleton<SerilogLogger>();
            
            // Алертинг
            services.AddSingleton<AlertService>();
            services.AddHostedService<MonitoringAlertService>();
        }
        
        private static void ConfigureHealthChecks(IServiceCollection services, IConfiguration configuration)
        {
            // Базовые проверки
            services.AddHealthChecks()
                .AddCheck<SystemResourcesHealthCheck>("system_resources", 
                    HealthStatus.Degraded, tags: new[] { "ready" })
                .AddUrlGroup(new Uri("https://www.vseinstrumenti.ru"), 
                    "vseinstrumenti.ru", HealthStatus.Degraded, tags: new[] { "ready" })
                .AddUrlGroup(new Uri("https://www.220-volt.ru"), 
                    "220-volt.ru", HealthStatus.Degraded, tags: new[] { "ready" });
            
            // Redis проверка
            var redisConnectionString = configuration.GetValue<string>("Redis:ConnectionString");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddSingleton<IConnectionMultiplexer>(sp => 
                    ConnectionMultiplexer.Connect(redisConnectionString));
                
                services.AddHealthChecks()
                    .AddCheck<RedisHealthCheck>("redis", 
                        HealthStatus.Unhealthy, tags: new[] { "ready" });
            }
            
            // Внешние сайты проверка
            services.AddHealthChecks()
                .AddCheck<ExternalSitesHealthCheck>("external_sites", 
                    HealthStatus.Degraded, tags: new[] { "ready" });
            
            // Проверка живости (базовая)
            services.AddHealthChecks()
                .AddCheck("live_check", () => 
                    HealthCheckResult.Healthy("Service is alive"), tags: new[] { "live" });
        }
        
        private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            
            var result = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.TotalSeconds,
                entries = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalSeconds,
                    description = e.Value.Description,
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data
                })
            };
            
            await context.Response.WriteAsJsonAsync(result);
        }
    }
}