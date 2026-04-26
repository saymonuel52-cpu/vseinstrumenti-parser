using System.Net;
using Microsoft.Extensions.Logging;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Сервис для работы с HttpClient (обёртка для обратной совместимости)
    ///推荐使用 IHtmlLoader для новых проектов
    /// </summary>
    public class HttpClientService : IDisposable
    {
        private readonly IHtmlLoader _htmlLoader;
        private readonly ILogger _logger;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с IHtmlLoader (рекомендуемый)
        /// </summary>
        public HttpClientService(IHtmlLoader htmlLoader, ILogger? logger = null)
        {
            _htmlLoader = htmlLoader ?? throw new ArgumentNullException(nameof(htmlLoader));
            _logger = logger ?? new ConsoleLogger();
        }

        /// <summary>
        /// Получить HTML-контент по URL (использует Polly политики)
        /// </summary>
        public async Task<string> GetHtmlAsync(string url, int maxRetries = 3, int initialDelay = 1000)
        {
            _logger.Log($"[{DateTime.Now:HH:mm:ss}] GET {url}");
            
            try
            {
                var html = await _htmlLoader.LoadHtmlAsync(url);
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Успешно получено {html.Length} байт");
                return html;
            }
            catch (Exception ex)
            {
                _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получить стандартный User-Agent
        /// </summary>
        private static string GetDefaultUserAgent()
        {
            return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Простой логгер в консоль (для backward compatibility)
    /// </summary>
    public interface ILogger
    {
        void Log(string message);
    }

    /// <summary>
    /// Реализация логгера в консоль
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}