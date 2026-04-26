using Microsoft.AspNetCore.SignalR;

namespace VseinstrumentiParser.WebUI.Services
{
    /// <summary>
    /// SignalR Hub for real-time parse progress updates
    /// </summary>
    public class ParseProgressHub : Hub
    {
        private readonly ILogger _logger;

        public ParseProgressHub(ILogger logger)
        {
            _logger = logger;
        }

        public async Task JoinJob(string jobId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
            _logger.LogInformation("Connection {ConnectionId} joined job group {JobId}", 
                Context.ConnectionId, jobId);
        }

        public async Task LeaveJob(string jobId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
            _logger.LogInformation("Connection {ConnectionId} left job group {JobId}", 
                Context.ConnectionId, jobId);
        }

        public async Task SendProgress(string jobId, double progress, int productsFound)
        {
            await Clients.Group($"job-{jobId}").SendAsync("ProgressUpdate", new
            {
                JobId = jobId,
                Progress = progress,
                ProductsFound = productsFound,
                Timestamp = DateTime.Now
            });
        }

        public async Task SendLog(string jobId, string message, string level)
        {
            await Clients.Group($"job-{jobId}").SendAsync("LogUpdate", new
            {
                JobId = jobId,
                Message = message,
                Level = level,
                Timestamp = DateTime.Now
            });
        }

        public async Task SendComplete(string jobId, bool success)
        {
            await Clients.Group($"job-{jobId}").SendAsync("JobComplete", new
            {
                JobId = jobId,
                Success = success,
                Timestamp = DateTime.Now
            });
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
