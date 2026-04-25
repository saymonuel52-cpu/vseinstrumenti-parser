using System;
using System.IO;
using System.Text;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Utilities
{
    /// <summary>
    /// Расширенный логгер с поддержкой файлового вывода и разных уровней логирования
    /// </summary>
    public class AdvancedLogger : ILogger, IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly StreamWriter _logWriter;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private readonly LogLevel _minLogLevel;
        private readonly bool _enableConsoleLogging;
        private readonly bool _enableFileLogging;
        private readonly bool _includeTimestamp;

        /// <summary>
        /// Уровни логирования
        /// </summary>
        public enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }

        /// <summary>
        /// Конструктор с настройками
        /// </summary>
        public AdvancedLogger(
            string logDirectory = "./logs",
            LogLevel minLogLevel = LogLevel.Information,
            bool enableConsoleLogging = true,
            bool enableFileLogging = true,
            bool includeTimestamp = true)
        {
            _logDirectory = logDirectory;
            _minLogLevel = minLogLevel;
            _enableConsoleLogging = enableConsoleLogging;
            _enableFileLogging = enableFileLogging;
            _includeTimestamp = includeTimestamp;

            // Создаем директорию для логов если не существует
            if (_enableFileLogging && !Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Создаем файл лога с текущей датой
            if (_enableFileLogging)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd");
                _logFilePath = Path.Combine(_logDirectory, $"parser_{timestamp}.log");
                
                // Открываем файл для записи (добавляем в конец)
                _logWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                
                LogInternal(LogLevel.Information, $"Логгер инициализирован. Файл лога: {_logFilePath}");
            }
            else
            {
                _logFilePath = string.Empty;
                _logWriter = null!;
            }
        }

        /// <summary>
        /// Конструктор с конфигурацией
        /// </summary>
        public AdvancedLogger(LoggingSettings settings)
            : this(
                logDirectory: settings.FileLogging?.Path?.Replace("{0:yyyyMMdd}", DateTime.Now.ToString("yyyyMMdd")) ?? "./logs",
                minLogLevel: ParseLogLevel(settings.LogLevel?.Default ?? "Information"),
                enableConsoleLogging: settings.ConsoleLogging?.Enabled ?? true,
                enableFileLogging: settings.FileLogging?.Enabled ?? true,
                includeTimestamp: settings.ConsoleLogging?.IncludeTimestamp ?? true)
        {
        }

        /// <summary>
        /// Логирование с указанием уровня
        /// </summary>
        public void Log(LogLevel level, string message)
        {
            if (level < _minLogLevel)
                return;

            LogInternal(level, message);
        }

        /// <summary>
        /// Логирование информационного сообщения
        /// </summary>
        public void LogInformation(string message)
        {
            Log(LogLevel.Information, message);
        }

        /// <summary>
        /// Логирование предупреждения
        /// </summary>
        public void LogWarning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        /// <summary>
        /// Логирование ошибки
        /// </summary>
        public void LogError(string message)
        {
            Log(LogLevel.Error, message);
        }

        /// <summary>
        /// Логирование отладочного сообщения
        /// </summary>
        public void LogDebug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        /// <summary>
        /// Логирование с исключением
        /// </summary>
        public void LogError(Exception exception, string? message = null)
        {
            var fullMessage = message != null 
                ? $"{message}: {exception.Message}" 
                : exception.Message;
            
            Log(LogLevel.Error, $"{fullMessage}\n{exception.StackTrace}");
        }

        /// <summary>
        /// Простое логирование (для обратной совместимости)
        /// </summary>
        public void Log(string message)
        {
            LogInformation(message);
        }

        /// <summary>
        /// Внутренний метод логирования
        /// </summary>
        private void LogInternal(LogLevel level, string message)
        {
            lock (_lock)
            {
                var timestamp = _includeTimestamp ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]" : "";
                var levelStr = GetLevelString(level);
                var formattedMessage = $"{timestamp} [{levelStr}] {message}";

                // Вывод в консоль
                if (_enableConsoleLogging)
                {
                    WriteToConsole(level, formattedMessage);
                }

                // Запись в файл
                if (_enableFileLogging && _logWriter != null)
                {
                    try
                    {
                        _logWriter.WriteLine(formattedMessage);
                    }
                    catch (Exception ex)
                    {
                        // Если не удалось записать в файл, выводим в консоль
                        Console.WriteLine($"Ошибка записи в лог-файл: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Вывод в консоль с цветами
        /// </summary>
        private void WriteToConsole(LogLevel level, string message)
        {
            var originalColor = Console.ForegroundColor;
            
            try
            {
                switch (level)
                {
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogLevel.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogLevel.Information:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case LogLevel.Debug:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    case LogLevel.Trace:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                }
                
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        /// <summary>
        /// Получить строковое представление уровня логирования
        /// </summary>
        private string GetLevelString(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// Парсинг уровня логирования из строки
        /// </summary>
        private static LogLevel ParseLogLevel(string level)
        {
            return level.ToUpper() switch
            {
                "TRACE" => LogLevel.Trace,
                "DEBUG" => LogLevel.Debug,
                "INFORMATION" or "INFO" => LogLevel.Information,
                "WARNING" or "WARN" => LogLevel.Warning,
                "ERROR" => LogLevel.Error,
                "CRITICAL" => LogLevel.Critical,
                _ => LogLevel.Information
            };
        }

        /// <summary>
        /// Получить статистику лог-файлов
        /// </summary>
        public LogStatistics GetStatistics()
        {
            if (!Directory.Exists(_logDirectory))
                return new LogStatistics { TotalLogFiles = 0, TotalSizeBytes = 0 };

            var logFiles = Directory.GetFiles(_logDirectory, "*.log");
            var totalSize = logFiles.Sum(f => new FileInfo(f).Length);

            return new LogStatistics
            {
                TotalLogFiles = logFiles.Length,
                TotalSizeBytes = totalSize,
                CurrentLogFile = _logFilePath,
                CurrentLogFileSize = File.Exists(_logFilePath) ? new FileInfo(_logFilePath).Length : 0
            };
        }

        /// <summary>
        /// Очистка старых лог-файлов
        /// </summary>
        public void CleanupOldLogs(int keepLastDays = 7, long maxTotalSizeMB = 100)
        {
            if (!Directory.Exists(_logDirectory))
                return;

            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                var cutoffDate = DateTime.Now.AddDays(-keepLastDays);
                var maxTotalSizeBytes = maxTotalSizeMB * 1024 * 1024;

                // Удаляем файлы старше keepLastDays дней
                var oldFiles = logFiles.Where(f => f.CreationTime < cutoffDate).ToList();
                foreach (var file in oldFiles)
                {
                    file.Delete();
                    LogInformation($"Удален старый лог-файл: {file.Name}");
                }

                // Если общий размер превышает лимит, удаляем самые старые файлы
                var currentTotalSize = logFiles.Where(f => f.Exists).Sum(f => f.Length);
                while (currentTotalSize > maxTotalSizeBytes && logFiles.Count > 1)
                {
                    var oldestFile = logFiles.Last();
                    if (oldestFile.Exists)
                    {
                        currentTotalSize -= oldestFile.Length;
                        oldestFile.Delete();
                        LogInformation($"Удален лог-файл для соблюдения лимита размера: {oldestFile.Name}");
                    }
                    logFiles.Remove(oldestFile);
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при очистке лог-файлов: {ex.Message}");
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _logWriter?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Статистика логирования
    /// </summary>
    public class LogStatistics
    {
        public int TotalLogFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public string? CurrentLogFile { get; set; }
        public long CurrentLogFileSize { get; set; }

        public double TotalSizeMB => TotalSizeBytes / (1024.0 * 1024.0);
        public double CurrentLogFileSizeMB => CurrentLogFileSize / (1024.0 * 1024.0);
    }

    /// <summary>
    /// Настройки логирования (для конфигурации)
    /// </summary>
    public class LoggingSettings
    {
        public Dictionary<string, string>? LogLevel { get; set; }
        public FileLoggingSettings? FileLogging { get; set; }
        public ConsoleLoggingSettings? ConsoleLogging { get; set; }
    }

    public class FileLoggingSettings
    {
        public bool Enabled { get; set; } = true;
        public string Path { get; set; } = "./logs/parser_{0:yyyyMMdd}.log";
        public int MaxFileSizeMB { get; set; } = 10;
        public int RetainedFileCount { get; set; } = 7;
    }

    public class ConsoleLoggingSettings
    {
        public bool Enabled { get; set; } = true;
        public bool IncludeTimestamp { get; set; } = true;
    }
}