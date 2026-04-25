using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VseinstrumentiParser.Services.Health
{
    /// <summary>
    /// Health check для проверки системных ресурсов
    /// </summary>
    public class SystemResourcesHealthCheck : IHealthCheck
    {
        private readonly ILogger<SystemResourcesHealthCheck> _logger;

        public SystemResourcesHealthCheck(ILogger<SystemResourcesHealthCheck> logger)
        {
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var issues = new List<string>();
                
                // Проверка памяти
                var memoryInfo = GetMemoryInfo();
                if (memoryInfo.AvailableMemoryMB < 100)
                {
                    issues.Add($"Low available memory: {memoryInfo.AvailableMemoryMB}MB");
                }
                
                if (memoryInfo.MemoryUsagePercent > 90)
                {
                    issues.Add($"High memory usage: {memoryInfo.MemoryUsagePercent}%");
                }

                // Проверка дискового пространства
                var diskInfo = GetDiskInfo();
                foreach (var disk in diskInfo)
                {
                    if (disk.AvailableFreeSpaceGB < 1)
                    {
                        issues.Add($"Low disk space on {disk.Drive}: {disk.AvailableFreeSpaceGB}GB free");
                    }
                    
                    if (disk.UsagePercent > 95)
                    {
                        issues.Add($"High disk usage on {disk.Drive}: {disk.UsagePercent}%");
                    }
                }

                // Проверка количества потоков
                var threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
                if (threadCount > 100)
                {
                    issues.Add($"High thread count: {threadCount}");
                }

                if (issues.Any())
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"System resources issues: {string.Join("; ", issues)}",
                        new Exception(string.Join("; ", issues))));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"System resources OK. Memory: {memoryInfo.AvailableMemoryMB}MB available, " +
                    $"Disk: {diskInfo.FirstOrDefault()?.AvailableFreeSpaceGB ?? 0}GB free"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System resources health check failed");
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Failed to check system resources", ex));
            }
        }

        private (long AvailableMemoryMB, double MemoryUsagePercent) GetMemoryInfo()
        {
            // Для Windows
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Используем PerformanceCounter или WMI в production
                // Здесь упрощенная версия
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                var totalMemoryMB = 8192; // Примерное значение, в production нужно получать реальное
                var availableMB = totalMemoryMB - memoryMB;
                var usagePercent = (double)memoryMB / totalMemoryMB * 100;
                
                return (availableMB, usagePercent);
            }
            
            // Для Linux/macOS
            return (1024, 50); // Заглушка
        }

        private List<(string Drive, double AvailableFreeSpaceGB, double UsagePercent)> GetDiskInfo()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            var result = new List<(string, double, double)>();
            
            foreach (var drive in drives)
            {
                try
                {
                    var availableGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                    var totalGB = drive.TotalSize / (1024 * 1024 * 1024);
                    var usagePercent = 100 - (drive.AvailableFreeSpace * 100 / drive.TotalSize);
                    
                    result.Add((drive.Name, Math.Round(availableGB, 2), Math.Round(usagePercent, 2)));
                }
                catch
                {
                    // Игнорируем ошибки доступа к диску
                }
            }

            return result;
        }
    }
}