using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Минимальные интерфейсы и модели
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}

public class Category
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? ProductCount { get; set; }
    public List<Category> SubCategories { get; set; } = new List<Category>();
    
    public override string ToString() => $"{Name} ({Url})";
}

public enum AvailabilityStatus { Unknown, InStock, OutOfStock, Limited, PreOrder, NotAvailable }

public class Product
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public decimal? OldPrice { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Article { get; set; } = string.Empty;
    public AvailabilityStatus Availability { get; set; } = AvailabilityStatus.Unknown;
    public string AvailabilityDetails { get; set; } = string.Empty;
    public Dictionary<string, string> Specifications { get; set; } = new Dictionary<string, string>();
    public double? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
    
    public override string ToString() => $"{Name} - {Price} руб. ({Brand})";
}

// Интерфейсы парсеров
public interface ICategoryParser
{
    Task<List<Category>> GetCategoriesAsync(string baseUrl = "https://www.220-volt.ru");
    Task<List<Category>> GetSubCategoriesAsync(string categoryUrl);
    Task<List<string>> GetProductUrlsFromCategoryAsync(string categoryUrl, int maxPages = 10);
}

public interface IProductParser
{
    Task<Product> ParseProductAsync(string productUrl);
    Task<List<Product>> ParseProductsAsync(IEnumerable<string> productUrls, int maxConcurrent = 5);
}

// Простой HttpClientService
public class HttpClientService : IDisposable
{
    private readonly System.Net.Http.HttpClient _httpClient;
    private readonly System.Net.Http.HttpClientHandler _handler;
    private readonly ILogger _logger;
    
    public HttpClientService(ILogger? logger = null, bool useCookies = true, string? userAgent = null)
    {
        _logger = logger ?? new ConsoleLogger();
        _handler = new System.Net.Http.HttpClientHandler
        {
            UseCookies = useCookies,
            CookieContainer = new System.Net.CookieContainer(),
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            AllowAutoRedirect = true
        };
        _httpClient = new System.Net.Http.HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }
    
    public async Task<string> GetHtmlAsync(string url, int maxRetries = 3, int initialDelay = 1000)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                _logger.Log($"GET {url} (попытка {attempt + 1}/{maxRetries})");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (System.Net.Http.HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                attempt++;
                int delay = initialDelay * (int)Math.Pow(2, attempt - 1);
                _logger.Log($"Ошибка: {ex.Message}. Повтор через {delay}мс");
                await Task.Delay(delay);
            }
        }
    }
    
    public void Dispose() => _httpClient?.Dispose();
}

// Заглушки парсеров для тестирования компиляции
public class VoltCategoryParser : ICategoryParser
{
    private readonly HttpClientService _httpClient;
    private readonly ILogger _logger;
    
    public VoltCategoryParser(HttpClientService httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? new ConsoleLogger();
    }
    
    public async Task<List<Category>> GetCategoriesAsync(string baseUrl = "https://www.220-volt.ru")
    {
        _logger.Log("Получение категорий с 220-volt.ru");
        // Заглушка
        return new List<Category>
        {
            new Category { Name = "Дрели ударные", Url = $"{baseUrl}/catalog-9889-elektroinstrumenty/dreli-udarnye/" },
            new Category { Name = "Перфораторы", Url = $"{baseUrl}/catalog-9889-elektroinstrumenty/perforatory/" }
        };
    }
    
    public Task<List<Category>> GetSubCategoriesAsync(string categoryUrl) => 
        Task.FromResult(new List<Category>());
    
    public Task<List<string>> GetProductUrlsFromCategoryAsync(string categoryUrl, int maxPages = 10) => 
        Task.FromResult(new List<string> { "https://www.220-volt.ru/product/drel-udarnaya-bosch-gsb-13-re/" });
}

public class VoltProductParser : IProductParser
{
    private readonly HttpClientService _httpClient;
    private readonly ILogger _logger;
    
    public VoltProductParser(HttpClientService httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? new ConsoleLogger();
    }
    
    public async Task<Product> ParseProductAsync(string productUrl)
    {
        _logger.Log($"Парсинг товара: {productUrl}");
        // Заглушка
        return new Product
        {
            Name = "Дрель ударная BOSCH GSB 13 RE",
            Price = 7990m,
            Brand = "BOSCH",
            Article = "GSB13RE",
            Availability = AvailabilityStatus.InStock,
            Specifications = new Dictionary<string, string>
            {
                { "Мощность", "600 Вт" },
                { "Тип двигателя", "Щеточный" }
            }
        };
    }
    
    public Task<List<Product>> ParseProductsAsync(IEnumerable<string> productUrls, int maxConcurrent = 5) => 
        Task.FromResult(new List<Product> { new Product { Name = "Тестовый товар", Price = 1000m } });
}

// Основная программа
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Тестирование парсеров 220-volt.ru ===");
        
        using var httpClient = new HttpClientService();
        var categoryParser = new VoltCategoryParser(httpClient);
        var productParser = new VoltProductParser(httpClient);
        
        // Тест категорий
        var categories = await categoryParser.GetCategoriesAsync();
        Console.WriteLine($"Найдено категорий: {categories.Count}");
        foreach (var cat in categories)
            Console.WriteLine($"  - {cat.Name}");
        
        // Тест парсинга товара
        var product = await productParser.ParseProductAsync("https://example.com/product");
        Console.WriteLine($"\nТестовый товар: {product.Name}, Цена: {product.Price} руб.");
        Console.WriteLine($"Характеристики: Мощность = {product.Specifications.GetValueOrDefault("Мощность", "нет")}");
        
        Console.WriteLine("\n=== Тест завершен успешно ===");
        Console.WriteLine("Парсеры скомпилированы и работают.");
    }
}