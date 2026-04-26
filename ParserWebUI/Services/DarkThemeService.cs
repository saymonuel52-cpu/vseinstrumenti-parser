using Microsoft.AspNetCore.Components;

namespace VseinstrumentiParser.WebUI.Services
{
    /// <summary>
    /// Service for managing dark/light theme
    /// </summary>
    public class DarkThemeService : IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private bool _isDark;
        private bool _disposed;

        public event Action? OnThemeChanged;

        public bool IsDark => _isDark;

        public DarkThemeService(IJSRuntime jsRuntime, IConfiguration configuration, ILogger logger)
        {
            _jsRuntime = jsRuntime;
            _configuration = configuration;
            _logger = logger;
            _isDark = configuration.GetValue<bool>("Theme:Dark", false);
        }

        public async Task ToggleTheme()
        {
            _isDark = !_isDark;
            await SaveThemePreference();
            await ApplyTheme();
            OnThemeChanged?.Invoke();
        }

        public async Task SetDarkMode(bool isDark)
        {
            if (_isDark != isDark)
            {
                _isDark = isDark;
                await SaveThemePreference();
                await ApplyTheme();
                OnThemeChanged?.Invoke();
            }
        }

        private async Task SaveThemePreference()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", _isDark ? "dark" : "light");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save theme preference to localStorage");
            }
        }

        private async Task ApplyTheme()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("setTheme", _isDark ? "dark" : "light");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply theme");
            }
        }

        public async Task LoadSavedPreference()
        {
            try
            {
                var savedTheme = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "theme");
                if (!string.IsNullOrEmpty(savedTheme))
                {
                    _isDark = savedTheme == "dark";
                    await ApplyTheme();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load saved theme preference");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                OnThemeChanged = null;
                _disposed = true;
            }
        }
    }
}
