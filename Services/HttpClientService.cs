using System.Net;
using System.Text;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Сервис для работы с HttpClient с поддержкой Cookie и повторных попыток
    /// </summary>
    public class HttpClientService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly HttpClientHandler _handler;
        private readonly ILogger _logger;
        private readonly RequestSettings _requestSettings;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с настройками
        /// </summary>
        /// <param name="logger">Логгер (опционально)</param>
        /// <param name="useCookies">Использовать CookieContainer</param>
        /// <param name="userAgent">User-Agent для запросов</param>
        public HttpClientService(ILogger? logger = null, bool useCookies = true, string? userAgent = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _requestSettings = new RequestSettings();
            
            _handler = new HttpClientHandler
            {
                UseCookies = useCookies,
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            _httpClient = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromSeconds(_requestSettings.TimeoutSeconds)
            };

            // Устанавливаем стандартные заголовки
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? _requestSettings.UserAgent);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        }

        /// <summary>
        /// Конструктор с конфигурацией RequestSettings
        /// </summary>
        public HttpClientService(ILogger logger, RequestSettings requestSettings, bool useCookies = true)
        {
            _logger = logger ?? new ConsoleLogger();
            _requestSettings = requestSettings ?? new RequestSettings();
            
            _handler = new HttpClientHandler
            {
                UseCookies = useCookies,
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            _httpClient = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromSeconds(_requestSettings.TimeoutSeconds)
            };

            // Устанавливаем стандартные заголовки
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_requestSettings.UserAgent);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        }

        /// <summary>
        /// Получить HTML-контент по URL
        /// </summary>
        /// <param name="url">URL для запроса</param>
        /// <param name="maxRetries">Максимальное количество повторных попыток</param>
        /// <param name="initialDelay">Начальная задержка для экспоненциальной backoff</param>
        /// <returns>HTML-контент в виде строки</returns>
        public async Task<string> GetHtmlAsync(string url, int maxRetries = 3, int initialDelay = 1000)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] GET {url} (попытка {attempt + 1}/{maxRetries})");
                    
                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Успешно получено {content.Length} байт");
                    
                    return content;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries - 1)
                {
                    attempt++;
                    int delay = initialDelay * (int)Math.Pow(2, attempt - 1);
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Ошибка: {ex.Message}. Повтор через {delay}мс");
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.Log($"[{DateTime.Now:HH:mm:ss}] Критическая ошибка: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Получить куки для домена
        /// </summary>
        /// <param name="domain">Домен</param>
        /// <returns>Список кук</returns>
        public List<Cookie> GetCookies(string domain)
        {
            return _handler.CookieContainer.GetCookies(new Uri(domain)).Cast<Cookie>().ToList();
        }

        /// <summary>
        /// Добавить куки
        /// </summary>
        /// <param name="domain">Домен</param>
        /// <param name="name">Имя куки</param>
        /// <param name="value">Значение</param>
        public void AddCookie(string domain, string name, string value)
        {
            _handler.CookieContainer.Add(new Uri(domain), new Cookie(name, value));
        }

        /// <summary>
        /// Очистить все куки
        /// </summary>
        public void ClearCookies()
        {
            _handler.CookieContainer = new CookieContainer();
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
                _httpClient?.Dispose();
                _handler?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Простой логгер в консоль
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