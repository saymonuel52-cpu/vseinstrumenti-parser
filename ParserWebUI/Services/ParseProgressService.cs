using System.Collections.Concurrent;

namespace VseinstrumentiParser.WebUI.Services
{
    /// <summary>
    /// Service for tracking parse job progress and real-time updates
    /// </summary>
    public class ParseProgressService
    {
        private readonly ConcurrentDictionary<string, ParseJobInfo> _jobs = new();
        private readonly ILogger _logger;

        public event EventHandler<ProgressUpdateEventArgs>? OnProgressUpdate;
        public event EventHandler<LogUpdateEventArgs>? OnLogUpdate;

        public ParseProgressService(ILogger logger)
        {
            _logger = logger;
        }

        public string StartJob(string source, string? category = null)
        {
            var jobId = Guid.NewGuid().ToString();
            _jobs[jobId] = new ParseJobInfo
            {
                Id = jobId,
                Source = source,
                Category = category,
                Status = "Running",
                StartTime = DateTime.Now,
                Progress = 0,
                ProductsFound = 0,
                Errors = 0
            };

            _logger.LogInformation("Parse job started: {JobId} - Source: {Source}, Category: {Category}", 
                jobId, source, category);

            return jobId;
        }

        public void UpdateProgress(string jobId, double progress, int productsFound, string? status = null)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Progress = progress;
                job.ProductsFound = productsFound;
                if (status != null) job.Status = status;

                OnProgressUpdate?.Invoke(this, new ProgressUpdateEventArgs
                {
                    JobId = jobId,
                    Progress = progress,
                    ProductsFound = productsFound
                });
            }
        }

        public void AddLog(string jobId, string message, string level = "Info")
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Logs.Add(new JobLogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                });

                // Keep only last 100 logs
                if (job.Logs.Count > 100)
                {
                    job.Logs.RemoveAt(0);
                }

                OnLogUpdate?.Invoke(this, new LogUpdateEventArgs
                {
                    JobId = jobId,
                    Message = message,
                    Level = level
                });
            }
        }

        public void CompleteJob(string jobId, bool success = true)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = success ? "Completed" : "Failed";
                job.EndTime = DateTime.Now;
                job.Progress = 100;

                _logger.LogInformation("Parse job completed: {JobId} - Status: {Status}, Products: {Products}", 
                    jobId, job.Status, job.ProductsFound);
            }
        }

        public void CancelJob(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Cancelled";
                job.EndTime = DateTime.Now;
            }
        }

        public ParseJobInfo? GetJobStatus(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return job;
        }

        public List<ParseJobInfo> GetAllJobs()
        {
            return _jobs.Values.OrderByDescending(j => j.StartTime).Take(50).ToList();
        }

        public void RemoveJob(string jobId)
        {
            _jobs.TryRemove(jobId, out _);
        }
    }

    public class ParseJobInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string Status { get; set; } = "Running";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double Progress { get; set; }
        public int ProductsFound { get; set; }
        public int Errors { get; set; }
        public List<JobLogEntry> Logs { get; set; } = new();

        public TimeSpan Duration => EndTime.HasValue 
            ? EndTime.Value - StartTime 
            : DateTime.Now - StartTime;
    }

    public class JobLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "Info";
        public string Message { get; set; } = string.Empty;
    }

    public class ProgressUpdateEventArgs : EventArgs
    {
        public string JobId { get; set; } = string.Empty;
        public double Progress { get; set; }
        public int ProductsFound { get; set; }
    }

    public class LogUpdateEventArgs : EventArgs
    {
        public string JobId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Level { get; set; } = "Info";
    }
}
