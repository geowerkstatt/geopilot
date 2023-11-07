using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Schedules validation jobs and provides access to status information for a specific job.
    /// </summary>
    public class ValidatorService : BackgroundService, IValidatorService
    {
        private readonly ILogger<ValidatorService> logger;
        private readonly Channel<(Guid Id, Func<CancellationToken, Task> Task)> queue;
        private readonly ConcurrentDictionary<Guid, (Status Status, string StatusMessage)> jobs = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatorService"/> class.
        /// </summary>
        public ValidatorService(ILogger<ValidatorService> logger)
        {
            this.logger = logger;
            queue = Channel.CreateUnbounded<(Guid, Func<CancellationToken, Task>)>();
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Parallel.ForEachAsync(queue.Reader.ReadAllAsync(stoppingToken), stoppingToken, async (item, cancellationToken) =>
            {
                try
                {
                    UpdateJobStatus(item.Id, Status.Processing, "Die Datei wird validiert...");
                    await item.Task(cancellationToken);
                    UpdateJobStatus(item.Id, Status.Completed, "Die Daten sind modellkonform.");
                }
                catch (Exception ex)
                {
                    var traceId = Guid.NewGuid();
                    UpdateJobStatus(item.Id, Status.Failed, $"Unbekannter Fehler. Fehler-Id: <{traceId}>");
                    logger.LogError(ex, "Unhandled exception TraceId: <{TraceId}> Message: <{ErrorMessage}>", traceId, ex.Message);
                }
            });
        }

        /// <inheritdoc/>
        public async Task EnqueueJobAsync(Guid jobId, Func<CancellationToken, Task> action)
        {
            UpdateJobStatus(jobId, Status.Enqueued, "Die Validierung wird vorbereitet...");
            await queue.Writer.WriteAsync((jobId, action));
        }

        /// <inheritdoc/>
        public (Status Status, string StatusMessage) GetJobStatusOrDefault(Guid jobId) =>
            jobs.TryGetValue(jobId, out var status) ? status : default;

        /// <summary>
        /// Adds or updates the status for the given <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId">The job identifier to be added or whose value should be updated.</param>
        /// <param name="status">The status.</param>
        /// <param name="statusMessage">The status message.</param>
        /// <param name="logMessage">Optional info log message.</param>
        private void UpdateJobStatus(Guid jobId, Status status, string statusMessage, string? logMessage = null)
        {
            jobs[jobId] = (status, statusMessage);
            if (!string.IsNullOrEmpty(logMessage)) logger.LogInformation(logMessage);
        }
    }
}
