using System.Collections.Concurrent;
using System.Threading.Channels;
using static GeoCop.Api.Validation.IValidatorService;

namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Schedules validation jobs and provides access to status information for a specific job.
    /// </summary>
    public class ValidatorService : BackgroundService, IValidatorService
    {
        private readonly ILogger<ValidatorService> logger;
        private readonly Channel<(Guid Id, ValidationAction Task)> queue;
        private readonly ConcurrentDictionary<Guid, ValidationJobStatus> jobs = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatorService"/> class.
        /// </summary>
        public ValidatorService(ILogger<ValidatorService> logger)
        {
            this.logger = logger;
            queue = Channel.CreateUnbounded<(Guid, ValidationAction)>();
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Parallel.ForEachAsync(queue.Reader.ReadAllAsync(stoppingToken), stoppingToken, async (item, cancellationToken) =>
            {
                try
                {
                    UpdateJobStatus(item.Id, Status.Processing, "Die Datei wird validiert...");
                    jobs[item.Id] = await item.Task(cancellationToken);
                }
                catch (ValidationFailedException ex)
                {
                    UpdateJobStatus(item.Id, Status.Failed, ex.Message);
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
        public async Task EnqueueJobAsync(Guid jobId, ValidationAction action)
        {
            UpdateJobStatus(jobId, Status.Enqueued, "Die Validierung wird vorbereitet...");
            await queue.Writer.WriteAsync((jobId, action));
        }

        /// <inheritdoc/>
        public ValidationJobStatus? GetJobStatusOrDefault(Guid jobId)
        {
            return jobs.TryGetValue(jobId, out var status) ? status : default;
        }

        /// <summary>
        /// Adds or updates the status for the given <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId">The job identifier to be added or whose value should be updated.</param>
        /// <param name="status">The status.</param>
        /// <param name="statusMessage">The status message.</param>
        private void UpdateJobStatus(Guid jobId, Status status, string statusMessage)
        {
            jobs[jobId] = new ValidationJobStatus(jobId, status, statusMessage);
        }
    }
}
