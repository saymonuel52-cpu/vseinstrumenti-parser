using System.Net;

namespace VseinstrumentiParser.WebUI.Services
{
    /// <summary>
    /// Middleware для проверки API ключа
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip for static files and health checks
            if (context.Request.Path.StartsWithSegments("/health") ||
                context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/_framework"))
            {
                await _next(context);
                return;
            }

            // Get API key from header
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            
            // Also check query string for some endpoints
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = context.Request.Query["api_key"].FirstOrDefault();
            }

            var validApiKey = _configuration["ApiKey"];
            
            if (string.IsNullOrEmpty(validApiKey))
            {
                _logger.LogWarning("API Key not configured in appsettings.json");
                await _next(context);
                return;
            }

            if (apiKey != validApiKey)
            {
                _logger.LogWarning($"Invalid API key attempt from {context.Connection.RemoteIpAddress}");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Invalid or missing API key\"}");
                return;
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Service for API key authentication
    /// </summary>
    public class ApiKeyAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public ApiKeyAuthenticationService(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string? GenerateApiKey()
        {
            var key = Guid.NewGuid().ToString("N");
            _logger.LogInformation("New API key generated: {Key}", key.Substring(0, 8) + "...");
            return key;
        }

        public bool ValidateApiKey(string apiKey)
        {
            var validKey = _configuration["ApiKey"];
            return apiKey == validKey;
        }
    }
}
