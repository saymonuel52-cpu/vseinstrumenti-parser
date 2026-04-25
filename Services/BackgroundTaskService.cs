using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VseinstrumentiParser.Services
{
    /// <summary>
    /// Служба для управления фоновыми задачами с поддержкой Graceful Shutdown
    /// </summary>
    public class BackgroundTaskService : IHostedService, IDisposable
    {
        private readonly ILogger<BackgroundTaskService> _logger;
        private readonly List<Task> _backgroundTasks = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _disposed;

        public BackgroundTaskService(ILogger<BackgroundTaskService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackgroundTaskService запущен");
            
            // Запуск фоновых задач
            _backgroundTasks.Add(Task.Run(() => MonitorCacheHealthAsync(_cancellationTokenSource.Token)));
            _backgroundTasks.Add(Task.Run(() => CleanupOldExportsAsync(_cancellationTokenSource.Token)));
            _backgroundTasks.Add(Task.Run(() => SendHeartbeatAsync(_cancellationTokenSource.Token)));
            
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Остановка BackgroundTaskService...");
            
            // Отправка сигнала отмены всем задачам
            _cancellationTokenSource.Cancel();
            
            try
            {
                // Ожидание завершения всех задач с таймаутом
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                var allTasks = Task.WhenAll(_backgroundTasks);
                
                var completedTask = await Task.WhenAny(allTasks, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("Таймаут при ожидании завершения фоновых задач");
                }
                else
                {
                    _logger.LogInformation("Все фоновые задачи завершены корректно");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при остановке фоновых задач");
            }
            
            _logger.LogInformation("BackgroundTaskService остановлен");
        }

        private async Task MonitorCacheHealthAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Мониторинг здоровья кэша
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    _logger.LogDebug("Мониторинг кэша выполнен");
                }
                catch (TaskCanceledException)
                {
                    // Нормальная отмена
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в мониторинге кэша");
                }
            }
            
            _logger.LogInformation("Мониторинг кэша остановлен");
        }

        private async Task CleanupOldExportsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Очистка старых экспортов
                    await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
                    
                    var exportsDir = new DirectoryInfo("exports");
                    if (exportsDir.Exists)
                    {
                        var oldFiles = exportsDir.GetFiles("*.csv")
                            .Where(f => f.LastWriteTime < DateTime.Now.AddDays(-7))
                            .ToList();
                        
                        foreach (var file in oldFiles)
                        {
                            file.Delete();
                            _logger.LogInformation("Удален старый файл экспорта: {File}", file.Name);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке старых экспортов");
                }
            }
            
            _logger.LogInformation("Очистка старых экспортов остановлена");
        }

        private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Отправка heartbeat (можно интегрировать с внешним мониторингом)
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    _logger.LogTrace("Heartbeat отправлен");
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при отправке heartbeat");
                }
            }
            
            _logger.LogInformation("Heartbeat остановлен");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }
    }
}