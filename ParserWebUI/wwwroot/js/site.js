// Global JavaScript functions for Blazor Web UI

// Theme management
function setTheme(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
    
    // Update Bootstrap classes
    const isDark = theme === 'dark';
    document.body.classList.toggle('bg-dark', isDark);
    document.body.classList.toggle('text-light', isDark);
}

// Get saved theme from localStorage
function getSavedTheme() {
    return localStorage.getItem('theme') || 'light';
}

// Initialize theme on page load
document.addEventListener('DOMContentLoaded', function() {
    const savedTheme = getSavedTheme();
    setTheme(savedTheme);
});

// Toast notification helper
function showToast(title, message, type = 'info') {
    const toastContainer = document.getElementById('toastContainer');
    if (!toastContainer) return;
    
    const toastTitle = document.getElementById('toastTitle');
    const toastBody = document.getElementById('toastBody');
    
    if (toastTitle && toastBody) {
        toastTitle.textContent = title;
        toastBody.textContent = message;
        
        // Update color based on type
        toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        
        const toastElement = new bootstrap.Toast(toastContainer);
        toastElement.show();
    }
}

// Confirm dialog
function confirm(message) {
    return window.confirm(message);
}

// Download file helper
function downloadFile(content, filename, mimeType = 'text/plain') {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

// Copy to clipboard
function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(function() {
        showToast('Success', 'Copied to clipboard', 'success');
    }, function(err) {
        showToast('Error', 'Failed to copy', 'error');
    });
}

// Auto-refresh interval management
let refreshIntervals = {};

function startAutoRefresh(elementId, intervalMs, callback) {
    if (refreshIntervals[elementId]) {
        stopAutoRefresh(elementId);
    }
    
    refreshIntervals[elementId] = setInterval(callback, intervalMs);
}

function stopAutoRefresh(elementId) {
    if (refreshIntervals[elementId]) {
        clearInterval(refreshIntervals[elementId]);
        delete refreshIntervals[elementId];
    }
}

// Chart.js helper
function createChart(canvasId, type, data, options) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return null;
    
    return new Chart(ctx, {
        type: type,
        data: data,
        options: options
    });
}

// SignalR connection helper
function createSignalRConnection(url) {
    return new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();
}

// Debounce function for search inputs
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Format bytes to human readable
function formatBytes(bytes, decimals = 2) {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

// Format duration
function formatDuration(seconds) {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    
    if (h > 0) {
        return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
    }
    if (m > 0) {
        return `${m}:${s.toString().padStart(2, '0')} мин`;
    }
    return `${s} сек`;
}

// Escape HTML to prevent XSS
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Initialize on load
window.blazorWebUI = {
    setTheme,
    getSavedTheme,
    showToast,
    confirm,
    downloadFile,
    copyToClipboard,
    startAutoRefresh,
    stopAutoRefresh,
    createChart,
    createSignalRConnection,
    debounce,
    formatBytes,
    formatDuration,
    escapeHtml
};
