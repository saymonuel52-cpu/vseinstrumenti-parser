namespace VseinstrumentiParser.Interfaces
{
    /// <summary>
    /// Интерфейс для загрузки HTML-контента с поддержкой resilience-политик
    /// </summary>
    public interface IHtmlLoader
    {
        /// <summary>
        /// Загрузить HTML по URL с автоматическими повторными попытками
        /// </summary>
        /// <param name="url">URL для загрузки</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>HTML-контент</returns>
        /// <exception cref="HttpRequestException">При превышении максимального числа попыток</exception>
        Task<string> LoadHtmlAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Загрузить HTML с кастомными заголовками
        /// </summary>
        Task<string> LoadHtmlAsync(string url, Dictionary<string, string>? headers, CancellationToken cancellationToken = default);
    }
}
