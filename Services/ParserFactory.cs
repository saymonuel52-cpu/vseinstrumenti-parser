using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VseinstrumentiParser.Interfaces;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Фабрика для создания парсеров в зависимости от источника данных
    /// </summary>
    public interface IParserFactory
    {
        ICategoryParser CreateCategoryParser(string source);
        IProductParser CreateProductParser(string source);
    }

    /// <summary>
    /// Реализация фабрики парсеров
    /// </summary>
    public class ParserFactory : IParserFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public ParserFactory(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public ICategoryParser CreateCategoryParser(string source)
        {
            return source.ToLower() switch
            {
                "vseinstrumenti" => _serviceProvider.GetRequiredService<CategoryParser>(),
                "220volt" => _serviceProvider.GetRequiredService<VoltCategoryParser>(),
                _ => throw new NotSupportedException($"Источник {source} не поддерживается")
            };
        }

        public IProductParser CreateProductParser(string source)
        {
            return source.ToLower() switch
            {
                "vseinstrumenti" => _serviceProvider.GetRequiredService<ProductParser>(),
                "220volt" => _serviceProvider.GetRequiredService<VoltProductParser>(),
                _ => throw new NotSupportedException($"Источник {source} не поддерживается")
            };
        }
    }
}