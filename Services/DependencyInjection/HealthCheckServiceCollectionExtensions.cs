using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using VseinstrumentiParser.Services.HealthChecks;

namespace VseinstrumentiParser.Services.DependencyInjection
{
    /// <summary>
    /// Расширения для регистрации Health Checks
    /// </summary>
    public static class HealthCheckServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрация Health Checks для критичных зависимостей
        /// </summary>
        public static IServiceCollection AddParserHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            // Health checks
            services.AddHealthChecks()
                // Domain availability check
                .AddCheck<DomainHealthCheck>(
                    "domains",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new[] { "ready" })
                // Redis connection check
                .AddCheck<RedisHealthCheck>(
                    "redis",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new[] { "ready", "live" });

            return services;
        }

        /// <summary>
        /// Добавление эндпоинтов для Health Checks
        /// </summary>
        public static void MapParserHealthChecks(this WebApplication app)
        {
            // /health - агрегированный статус
            app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthCheckOptions
            {
                Predicate = _ => true
            });

            // /health/live - только проверки для liveness (приложение работает)
            app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live"),
                ResponseWriter = Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckResponseWriter.WriteResponse
            });

            // /health/ready - только проверки для readiness (готов к работе)
            app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckResponseWriter.WriteResponse
            });
        }
    }
}
