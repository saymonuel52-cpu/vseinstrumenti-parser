using System.Text.Json;
using VseinstrumentiParser.Models.Configuration;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Сервис для работы с конфигурацией приложения
    /// </summary>
    public class ConfigurationService
    {
        private readonly string _configFilePath;
        private AppSettings? _appSettings;
        private readonly ILogger _logger;

        /// <summary>
        /// Конструктор с указанием пути к файлу конфигурации
        /// </summary>
        public ConfigurationService(ILogger logger, string configFilePath = "appsettings.json")
        {
            _logger = logger;
            _configFilePath = configFilePath;
        }

        /// <summary>
        /// Загружает конфигурацию из файла
        /// </summary>
        public AppSettings LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger.Log($"Файл конфигурации {_configFilePath} не найден. Создаю конфигурацию по умолчанию.");
                    _appSettings = CreateDefaultConfiguration();
                    SaveConfiguration(_appSettings);
                    return _appSettings;
                }

                var json = File.ReadAllText(_configFilePath);
                _appSettings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (_appSettings == null)
                {
                    _logger.Log($"Не удалось десериализовать конфигурацию из {_configFilePath}. Использую конфигурацию по умолчанию.");
                    _appSettings = CreateDefaultConfiguration();
                }

                _logger.Log($"Конфигурация успешно загружена из {_configFilePath}");
                return _appSettings;
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при загрузке конфигурации: {ex.Message}");
                _appSettings = CreateDefaultConfiguration();
                return _appSettings;
            }
        }

        /// <summary>
        /// Сохраняет конфигурацию в файл
        /// </summary>
        public void SaveConfiguration(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_configFilePath, json);
                _logger.Log($"Конфигурация успешно сохранена в {_configFilePath}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при сохранении конфигурации: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает текущую конфигурацию (загружает если еще не загружена)
        /// </summary>
        public AppSettings GetConfiguration()
        {
            return _appSettings ?? LoadConfiguration();
        }

        /// <summary>
        /// Создает конфигурацию по умолчанию
        /// </summary>
        private AppSettings CreateDefaultConfiguration()
        {
            return new AppSettings
            {
                ParserSettings = new ParserSettings
                {
                    BaseUrls = new BaseUrlsSettings(),
                    RequestSettings = new RequestSettings(),
                    ParsingLimits = new ParsingLimits(),
                    ExportSettings = new ExportSettings()
                }
            };
        }
    }

    /// <summary>
    /// Полная конфигурация приложения
    /// </summary>
    public class AppSettings
    {
        public ParserSettings ParserSettings { get; set; } = new ParserSettings();
    }
}