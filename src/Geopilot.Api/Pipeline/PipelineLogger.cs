namespace Geopilot.Api.Pipeline;

internal sealed class PipelineLogger : ILogger
{
    private readonly ILogger innerLogger;
    private readonly string messagePrefix;

    private PipelineLogger(ILogger innerLogger, string messagePrefix)
    {
        this.innerLogger = innerLogger;
        this.messagePrefix = messagePrefix;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return innerLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return innerLogger.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!innerLogger.IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        innerLogger.Log(
            logLevel,
            eventId,
            $"[{messagePrefix}] {message}",
            exception,
            static (s, _) => s);
    }

    public static PipelineLoggerBuilder Builder()
    {
        return new PipelineLoggerBuilder();
    }

    internal class PipelineLoggerBuilder
    {
        private ILogger? logger;
        private Guid? jobId;
        private string? pipelineId;
        private string? stepId;

        public PipelineLoggerBuilder Logger(ILogger logger)
        {
            this.logger = logger;
            return this;
        }

        public PipelineLoggerBuilder JobId(Guid jobId)
        {
            this.jobId = jobId;
            return this;
        }

        public PipelineLoggerBuilder PipelineId(string pipelineId)
        {
            this.pipelineId = pipelineId;
            return this;
        }

        public PipelineLoggerBuilder StepId(string stepId)
        {
            this.stepId = stepId;
            return this;
        }

        public PipelineLogger Build()
        {
            var jobIdPrevix = jobId != null ? $"JobId: {jobId}" : string.Empty;
            var stepIdPrevix = !string.IsNullOrEmpty(stepId) ? $"Step-Id: {stepId}" : string.Empty;
            var pipelineIdPrevix = !string.IsNullOrEmpty(pipelineId) ? $"Pipeline-Id: {pipelineId}" : string.Empty;
            var messagePrefix = string.Join(", ", new[] { jobIdPrevix, pipelineIdPrevix, stepIdPrevix }.Where(s => !string.IsNullOrEmpty(s)));
            return new PipelineLogger(logger ?? throw new InvalidOperationException("Logger must be provided"), messagePrefix);
        }
    }
}
