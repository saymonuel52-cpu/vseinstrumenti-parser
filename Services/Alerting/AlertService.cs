using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VseinstrumentiParser.Services.Alerting
{
    /// <summary>
    /// Служба отправки уведомлений в различные каналы
    /// </summary>
    public class AlertService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AlertService> _logger;
        private readonly HttpClient _httpClient;

        public AlertService(IConfiguration configuration, ILogger<AlertService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Отправить уведомление об ошибке
        /// </summary>
        public async Task SendErrorAlertAsync(string title, string message, Exception? exception = null)
        {
            var fullMessage = $"{title}\n\n{message}";
            
            if (exception != null)
            {
                fullMessage += $"\n\nИсключение: {exception.Message}\nСтек: {exception.StackTrace}";
            }

            await SendToAllChannelsAsync("🚨 ОШИБКА", fullMessage);
        }

        /// <summary>
        /// Отправить уведомление о предупреждении
        /// </summary>
        public async Task SendWarningAlertAsync(string title, string message)
        {
            await SendToAllChannelsAsync("⚠️ ПРЕДУПРЕЖДЕНИЕ", $"{title}\n\n{message}");
        }

        /// <summary>
        /// Отправить информационное уведомление
        /// </summary>
        public async Task SendInfoAlertAsync(string title, string message)
        {
            await SendToAllChannelsAsync("ℹ️ ИНФОРМАЦИЯ", $"{title}\n\n{message}");
        }

        /// <summary>
        /// Отправить уведомление об успешном выполнении
        /// </summary>
        public async Task SendSuccessAlertAsync(string title, string message)
        {
            await SendToAllChannelsAsync("✅ УСПЕХ", $"{title}\n\n{message}");
        }

        /// <summary>
        /// Отправить уведомление во все настроенные каналы
        /// </summary>
        private async Task SendToAllChannelsAsync(string prefix, string message)
        {
            var tasks = new List<Task>();
            
            // Slack
            var slackWebhookUrl = _configuration["Monitoring:Alerting:SlackWebhookUrl"];
            if (!string.IsNullOrEmpty(slackWebhookUrl))
            {
                tasks.Add(SendToSlackAsync(prefix, message, slackWebhookUrl));
            }

            // Telegram
            var telegramBotToken = _configuration["Monitoring:Alerting:TelegramBotToken"];
            var telegramChatId = _configuration["Monitoring:Alerting:TelegramChatId"];
            if (!string.IsNullOrEmpty(telegramBotToken) && !string.IsNullOrEmpty(telegramChatId))
            {
                tasks.Add(SendToTelegramAsync(prefix, message, telegramBotToken, telegramChatId));
            }

            // Email
            var emailEnabled = _configuration.GetValue<bool>("Monitoring:Alerting:Email:Enabled", false);
            if (emailEnabled)
            {
                tasks.Add(SendToEmailAsync(prefix, message));
            }

            // Логирование
            _logger.LogInformation("Alert: {Prefix} - {Message}", prefix, message);

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке уведомлений");
            }
        }

        /// <summary>
        /// Отправить уведомление в Slack
        /// </summary>
        private async Task SendToSlackAsync(string prefix, string message, string webhookUrl)
        {
            try
            {
                var payload = new
                {
                    text = $"{prefix}: {message}",
                    username = "Vseinstrumenti Parser",
                    icon_emoji = ":robot_face:",
                    channel = _configuration["Monitoring:Alerting:SlackChannel"] ?? "#alerts"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                response.EnsureSuccessStatusCode();
                
                _logger.LogDebug("Уведомление отправлено в Slack");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке уведомления в Slack");
            }
        }

        /// <summary>
        /// Отправить уведомление в Telegram
        /// </summary>
        private async Task SendToTelegramAsync(string prefix, string message, string botToken, string chatId)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
                
                var payload = new
                {
                    chat_id = chatId,
                    text = $"*{prefix}*\n{message}",
                    parse_mode = "Markdown",
                    disable_web_page_preview = true
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                _logger.LogDebug("Уведомление отправлено в Telegram");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке уведомления в Telegram");
            }
        }

        /// <summary>
        /// Отправить уведомление по Email
        /// </summary>
        private async Task SendToEmailAsync(string prefix, string message)
        {
            try
            {
                var smtpServer = _configuration["Monitoring:Alerting:EmailSmtpServer"];
                var from = _configuration["Monitoring:Alerting:EmailFrom"];
                var to = _configuration["Monitoring:Alerting:EmailTo"];
                
                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                {
                    return;
                }

                // В production используйте библиотеку типа MailKit
                // Здесь упрощенная реализация
                _logger.LogInformation("Email alert would be sent: {Prefix} - {Message}", prefix, message);
                
                // Реализация отправки email зависит от выбранной библиотеки
                // Для примера просто логируем
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке email уведомления");
            }
        }

        /// <summary>
        /// Отправить уведомление о состоянии здоровья
        /// </summary>
        public async Task SendHealthAlertAsync(string status, Dictionary<string, string> details)
        {
            var message = $"Статус здоровья: {status}\n\n";
            
            foreach (var detail in details)
            {
                message += $"{detail.Key}: {detail.Value}\n";
            }

            if (status == "Unhealthy")
            {
                await SendErrorAlertAsync("Health Check Failed", message);
            }
            else if (status == "Degraded")
            {
                await SendWarningAlertAsync("Health Check Degraded", message);
            }
            else
            {
                await SendInfoAlertAsync("Health Check Healthy", message);
            }
        }

        /// <summary>
        /// Отправить уведомление о превышении лимитов
        /// </summary>
        public async Task SendRateLimitAlertAsync(string resource, int current, int limit)
        {
            var message = $"Ресурс: {resource}\nТекущее значение: {current}\nЛимит: {limit}\nИспользование: {(double)current / limit * 100:F1}%";
            
            await SendWarningAlertAsync("Превышение лимита", message);
        }

        /// <summary>
        /// Отправить уведомление о завершении парсинга
        /// </summary>
        public async Task SendParsingCompleteAlertAsync(string category, int productsCount, TimeSpan duration)
        {
            var message = $"Категория: {category}\nТоваров: {productsCount}\nВремя выполнения: {duration:hh\\:mm\\:ss}";
            
            await SendSuccessAlertAsync("Парсинг завершен", message);
        }
    }
}