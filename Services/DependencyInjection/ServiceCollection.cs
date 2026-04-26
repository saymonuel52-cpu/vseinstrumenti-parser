using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VseinstrumentiParser.Interfaces;
using VseinstrumentiParser.Models.Configuration;
using VseinstrumentiParser.Services;
using VseinstrumentiParser.Utilities;

namespace VseinstrumentiParser.Services.DependencyInjection
{
    /// <summary>
    /// Простой контейнер внедрения зависимостей
    /// </summary>
    public class ServiceCollection
    {
        private readonly Dictionary<Type, Func<object>> _services = new Dictionary<Type, Func<object>>();
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly object _lock = new object();

        /// <summary>
        /// Регистрирует сервис как синглтон
        /// </summary>
        public void AddSingleton<TService, TImplementation>() where TImplementation : TService, new()
        {
            _services[typeof(TService)] = () => Activator.CreateInstance<TImplementation>();
        }

        /// <summary>
        /// Регистрирует сервис как синглтон с фабрикой
        /// </summary>
        public void AddSingleton<TService>(Func<IServiceProvider, TService> factory)
        {
            _services[typeof(TService)] = () => factory(this);
        }

        /// <summary>
        /// Регистрирует сервис как транзиентный
        /// </summary>
        public void AddTransient<TService, TImplementation>() where TImplementation : TService, new()
        {
            _services[typeof(TService)] = () => Activator.CreateInstance<TImplementation>();
        }

        /// <summary>
        /// Получает сервис по типу
        /// </summary>
        public TService GetService<TService>()
        {
            return (TService)GetService(typeof(TService));
        }

        /// <summary>
        /// Получает сервис по типу
        /// </summary>
        public object GetService(Type serviceType)
        {
            lock (_lock)
            {
                if (_singletons.TryGetValue(serviceType, out var singleton))
                {
                    return singleton;
                }

                if (_services.TryGetValue(serviceType, out var factory))
                {
                    var instance = factory();
                    
                    // Кэшируем синглтоны
                    if (IsSingleton(serviceType))
                    {
                        _singletons[serviceType] = instance;
                    }
                    
                    return instance;
                }

                throw new InvalidOperationException($"Сервис типа {serviceType.Name} не зарегистрирован");
            }
        }

        /// <summary>
        /// Создает провайдер сервисов
        /// </summary>
        public IServiceProvider BuildServiceProvider()
        {
            return this;
        }

        /// <summary>
        /// Проверяет, является ли сервис синглтоном
        /// </summary>
        private bool IsSingleton(Type serviceType)
        {
            // В нашей простой реализации все зарегистрированные сервисы считаются синглтонами
            return true;
        }
    }

    /// <summary>
    /// Расширения для регистрации сервисов парсера
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрирует все сервисы парсера
        /// </summary>
        public static ServiceCollection AddParserServices(this ServiceCollection services, ILogger logger)
        {
            // Регистрация конфигурации
            services.AddSingleton<ConfigurationService>(sp => new ConfigurationService(logger));
            services.AddSingleton<AppSettings>(sp =>
            {
                var configService = sp.GetService<ConfigurationService>();
                return configService.GetConfiguration();
            });

            // Регистрация утилит
            services.AddSingleton<ILogger>(sp => logger);
            services.AddSingleton<RetryPolicy>(sp => RetryPolicy.CreateForParsing(logger));

            // Регистрация HTTP-клиента
            services.AddSingleton<HttpClientService>(sp =>
            {
                var config = sp.GetService<AppSettings>();
                return new HttpClientService(logger, config.ParserSettings.RequestSettings);
            });

            // Регистрация парсеров
            services.AddSingleton<ICategoryParser, CategoryParser>();
            services.AddSingleton<IProductParser, ProductParser>();
            services.AddSingleton<VseinstrumentiParserService>();
            services.AddSingleton<VoltParserService>();
            
            // Регистрация парсеров для 220-volt.ru
            services.AddSingleton<VoltCategoryParser>();
            services.AddSingleton<VoltProductParser>();
            
            // Регистрация фабрики парсеров
            services.AddSingleton<IParserFactory, ParserFactory>();

            // Регистрация сервисов экспорта
            services.AddSingleton<ExportService>();

            return services;
        }
    }

    /// <summary>
    /// Интерфейс провайдера сервисов
    /// </summary>
    public interface IServiceProvider
    {
        TService GetService<TService>();
        object GetService(Type serviceType);
    }
}