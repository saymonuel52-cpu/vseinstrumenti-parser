using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models.Configuration;
using VseinstrumentiParser.Services;

namespace VseinstrumentiParser.Services.DependencyInjection
{
    /// <summary>
    /// Расширения для регистрации HTTP-клиентов и политик устойчивости
    /// </summary>
    public static class HttpClientServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрация типизированных HTTP-клиентов с Polly политиками
        /// </summary>
        public static IServiceCollection AddParserHttpClient(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind settings
            services.Configure<HttpClientSettings>(configuration.GetSection("HttpClientSettings"));
            services.AddSingleton(sp =>
                sp.GetRequiredService<IOptions<HttpClientSettings>>().Value);

            // Add typed client with Polly policies
            services.AddHttpClient("ParserClient")
                .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | 
                                            System.Net.DecompressionMethods.Deflate,
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5,
                    UseCookies = false
                })
                .AddPolicyHandler(sp =>
                {
                    var settings = sp.GetRequiredService<HttpClientSettings>();
                    
                    // Timeout policy
                    return Policy.TimeoutAsync(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                })
                .AddPolicyHandler(sp =>
                {
                    var settings = sp.GetRequiredService<HttpClientSettings>();
                    
                    // Retry policy
                    return Policy
                        .Handle<HttpRequestException>()
                        .WaitAndRetryAsync(
                            settings.MaxRetryAttempts,
                            attempt => TimeSpan.FromMilliseconds(
                                Math.Min(settings.InitialRetryDelayMs * Math.Pow(2, attempt - 1), settings.MaxRetryDelayMs)));
                });

            // Register core services
            services.AddSingleton<RequestPolicyExecutor>();
            services.AddSingleton<DataSanitizer>();
            services.AddSingleton<IHtmlLoader, HtmlLoader>();

            return services;
        }
    }
}
