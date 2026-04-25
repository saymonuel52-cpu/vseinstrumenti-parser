using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.File;
using Serilog.Sinks.Console;
using Serilog.Sinks.Seq;
using Serilog.Context;
using Serilog.Core;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Utilities
{
    /// <summary>
    /// Адаптер Serilog для работы с интерфейсом ILogger
    /// </summary>
    public class SerilogLogger : ILogger, IDisposable
    {
        private readonly ILogger _serilog;
        private readonly SerilogSettings _settings;
        private readonly LoggingLevelSwitch _levelSwitch;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с настройками Serilog
        /// </summary>
        public SerilogLogger(SerilogSettings settings)
        {
            _settings = settings;
            _levelSwitch = new LoggingLevelSwitch(ParseLogLevel(settings.MinimumLevel));

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", settings.ApplicationName)
                .Enrich.WithProperty("Environment", settings.Environment)
                .Enrich.WithProperty("Version", settings.Version);

            // Консольный вывод
            if (settings.ConsoleEnabled)
            {
                loggerConfiguration.WriteTo.Console(
                    outputTemplate: settings.ConsoleOutputTemplate,
                    restrictedToMinimumLevel: ParseLogLevel(settings.ConsoleMinimumLevel));
            }

            // Файловый вывод
            if (settings.FileEnabled && !string.IsNullOrEmpty(settings.FilePath))
            {
                var path = settings.FilePath.Replace("{Date}", DateTime.Now.ToString("yyyyMMdd"));
                
                if (settings.FileFormat == "json")
                {
                    loggerConfiguration.WriteTo.File(
                        new JsonFormatter(),
                        path,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: settings.RetainedFileCountLimit,
                        fileSizeLimitBytes: settings.FileSizeLimitBytes,
                        restrictedToMinimumLevel: ParseLogLevel(settings.FileMinimumLevel));
                }
                else
                {
                    loggerConfiguration.WriteTo.File(
                        path,
                        outputTemplate: settings.FileOutputTemplate,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: settings.RetainedFileCountLimit,
                        fileSizeLimitBytes: settings.FileSizeLimitBytes,
                        restrictedToMinimumLevel: ParseLogLevel(settings.FileMinimumLevel));
                }
            }

            // Seq вывод
            if (settings.SeqEnabled && !string.IsNullOrEmpty(settings.SeqUrl))
            {
                loggerConfiguration.WriteTo.Seq(
                    settings.SeqUrl,
                    apiKey: settings.SeqApiKey,
                    restrictedToMinimumLevel: ParseLogLevel(settings.SeqMinimumLevel));
            }

            // Application Insights
            if (settings.ApplicationInsightsEnabled && !string.IsNullOrEmpty(settings.ApplicationInsightsConnectionString))
            {
                // Для Application Insights потребуется пакет Serilog.Sinks.ApplicationInsights
                // loggerConfiguration.WriteTo.ApplicationInsights(settings.ApplicationInsightsConnectionString, TelemetryConverter.Traces);
            }

            _serilog = loggerConfiguration.CreateLogger();
            
            LogInformation($"SerilogLogger инициализирован. Уровень: {_levelSwitch.MinimumLevel}, Консоль: {settings.ConsoleEnabled}, Файл: {settings.FileEnabled}");
        }

        /// <summary>
        /// Конструктор с минимальными настройками
        /// </summary>
        public SerilogLogger(string applicationName = "VseinstrumentiParser")
            : this(new SerilogSettings
            {
                ApplicationName = applicationName,
                ConsoleEnabled = true,
                FileEnabled = true,
                FilePath = "./logs/log-.json",
                FileFormat = "json",
                MinimumLevel = "Information"
            })
        {
        }

        /// <summary>
        /// Парсинг уровня логирования
        /// </summary>
        private LogEventLevel ParseLogLevel(string level)
        {
            return level.ToUpper() switch
            {
                "VERBOSE" or "TRACE" => LogEventLevel.Verbose,
                "DEBUG" => LogEventLevel.Debug,
                "INFORMATION" or "INFO" => LogEventLevel.Information,
                "WARNING" or "WARN" => LogEventLevel.Warning,
                "ERROR" => LogEventLevel.Error,
                "FATAL" or "CRITICAL" => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }

        /// <summary>
        /// Логирование с уровнем
        /// </summary>
        public void Log(LogLevel level, string message)
        {
            var logEventLevel = MapLogLevel(level);
            _serilog.Write(logEventLevel, message);
        }

        /// <summary>
        /// Логирование информационного сообщения
        /// </summary>
        public void LogInformation(string message)
        {
            _serilog.Information(message);
        }

        /// <summary>
        /// Логирование предупреждения
        /// </summary>
        public void LogWarning(string message)
        {
            _serilog.Warning(message);
        }

        /// <summary>
        /// Логирование ошибки
        /// </summary>
        public void LogError(string message)
        {
            _serilog.Error(message);
        }

        /// <summary>
        /// Логирование ошибки с исключением
        /// </summary>
        public void LogError(Exception exception, string? message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                _serilog.Error(exception, exception.Message);
            }
            else
            {
                _serilog.Error(exception, message);
            }
        }

        /// <summary>
        /// Логирование отладочного сообщения
        /// </summary>
        public void LogDebug(string message)
        {
            _serilog.Debug(message);
        }

        /// <summary>
        /// Логирование с контекстом
        /// </summary>
        public IDisposable WithContext(string key, object value)
        {
            return LogContext.PushProperty(key, value);
        }

        /// <summary>
        /// Логирование с несколькими свойствами контекста
        /// </summary>
        public IDisposable WithContext(Dictionary<string, object> properties)
        {
            var disposables = new List<IDisposable>();
            foreach (var property in properties)
            {
                disposables.Add(LogContext.PushProperty(property.Key, property.Value));
            }
            
            return new CompositeDisposable(disposables);
        }

        /// <summary>
        /// Простое логирование (для обратной совместимости)
        /// </summary>
        public void Log(string message)
        {
            LogInformation(message);
        }

        /// <summary>
        /// Маппинг уровней логирования
        /// </summary>
        private LogEventLevel MapLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }

        /// <summary>
        /// Изменить минимальный уровень логирования
        /// </summary>
        public void SetMinimumLevel(LogLevel level)
        {
            _levelSwitch.MinimumLevel = MapLogLevel(level);
        }

        /// <summary>
        /// Получить статистику логирования
        /// </summary>
        public SerilogStatistics GetStatistics()
        {
            // В реальной реализации здесь можно собирать статистику из Serilog
            return new SerilogStatistics
            {
                MinimumLevel = _levelSwitch.MinimumLevel.ToString(),
                IsConsoleEnabled = _settings.ConsoleEnabled,
                IsFileEnabled = _settings.FileEnabled,
                FilePath = _settings.FilePath,
                FileFormat = _settings.FileFormat
            };
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                (_serilog as IDisposable)?.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Класс для композитного disposable
        /// </summary>
        private class CompositeDisposable : IDisposable
        {
            private readonly List<IDisposable> _disposables;

            public CompositeDisposable(List<IDisposable> disposables)
            {
                _disposables = disposables;
            }

            public void Dispose()
            {
                foreach (var disposable in _disposables)
                {
                    disposable.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Настройки Serilog
    /// </summary>
    public class SerilogSettings
    {
        public string ApplicationName { get; set; } = "VseinstrumentiParser";
        public string Environment { get; set; } = "Production";
        public string Version { get; set; } = "1.0.0";
        
        public string MinimumLevel { get; set; } = "Information";
        
        // Console settings
        public bool ConsoleEnabled { get; set; } = true;
        public string ConsoleMinimumLevel { get; set; } = "Information";
        public string ConsoleOutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        
        // File settings
        public bool FileEnabled { get; set; } = true;
        public string FilePath { get; set; } = "./logs/log-{Date}.json";
        public string FileFormat { get; set; } = "json"; // json or text
        public string FileMinimumLevel { get; set; } = "Information";
        public string FileOutputTemplate { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public int RetainedFileCountLimit { get; set; } = 31;
        public long FileSizeLimitBytes { get; set; } = 1073741824; // 1 GB
        
        // Seq settings
        public bool SeqEnabled { get; set; } = false;
        public string SeqUrl { get; set; } = "http://localhost:5341";
        public string SeqApiKey { get; set; } = "";
        public string SeqMinimumLevel { get; set; } = "Information";
        
        // Application Insights settings
        public bool ApplicationInsightsEnabled { get; set; } = false;
        public string ApplicationInsightsConnectionString { get; set; } = "";
    }

    /// <summary>
    /// Статистика Serilog
    /// </summary>
    public class SerilogStatistics
    {
        public string MinimumLevel { get; set; } = string.Empty;
        public bool IsConsoleEnabled { get; set; }
        public bool IsFileEnabled { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public DateTimeOffset? LastLogTime { get; set; }
    }

    /// <summary>
    /// Расширения для регистрации в DI
    /// </summary>
    public static class SerilogExtensions
    {
        public static IServiceCollection AddSerilogLogger(
            this IServiceCollection services,
            SerilogSettings settings)
        {
            services.AddSingleton(settings);
            services.AddSingleton<ILogger, SerilogLogger>();
            
            return services;
        }

        public static IServiceCollection AddSerilogWithConsole(
            this IServiceCollection services,
            string applicationName = "VseinstrumentiParser")
        {
            var settings = new SerilogSettings
            {
                ApplicationName = applicationName,
                ConsoleEnabled = true,
                FileEnabled = false,
                MinimumLevel = "Information"
            };
            
            return services.AddSerilogLogger(settings);
        }

        public static IServiceCollection AddSerilogWithJsonFile(
            this IServiceCollection services,
            string applicationName = "VseinstrumentiParser")
        {
            var settings = new SerilogSettings
            {
                ApplicationName = applicationName,
                ConsoleEnabled = true,
                FileEnabled = true,
                FilePath = "./logs/log-.json",
                FileFormat = "json",
                MinimumLevel = "Information"
            };
            
            return services.AddSerilogLogger(settings);
        }

        public static IServiceCollection AddSerilogWithSeq(
            this IServiceCollection services,
            string seqUrl = "http://localhost:5341",
            string applicationName = "VseinstrumentiParser")
        {
            var settings = new SerilogSettings
            {
                ApplicationName = applicationName,
                ConsoleEnabled = true,
                FileEnabled = true,
                FileFormat = "json",
                SeqEnabled = true,
                SeqUrl = seqUrl,
                MinimumLevel = "Debug"
            };
            
            return services.AddSerilogLogger(settings);
        }
    }
}