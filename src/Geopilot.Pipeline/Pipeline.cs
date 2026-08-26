using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline;

/// <summary>
/// Represents a sequence of processing steps and associated parameters to be executed as a pipeline.
/// </summary>
/// <remarks>A pipeline consists of an ordered collection of steps, each of which performs a specific operation.
/// Optionally, parameters can be provided to configure the behavior of the pipeline or its steps.</remarks>
internal sealed class Pipeline : IPipeline
{
    // An int because Interlocked has no bool overload; 1 means disposed.
    private int disposed;

    /// <inheritdoc/>
    public void Dispose()
    {
        // Claimed atomically ahead of the cleanup below: the runner's cleanup and the job retirement
        // dispose independently, and a cleanup which partially fails must still run at most once.
        if (Interlocked.Exchange(ref disposed, 1) == 1)
            return;

        foreach (var step in Steps)
        {
            try
            {
                step.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispose step <{Step}> of pipeline <{Pipeline}>.", step.Id, Id);
            }
        }

        try
        {
            if (Path.Exists(pipelineFileDirectory))
                Directory.Delete(pipelineFileDirectory, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete the working directory <{Directory}> of pipeline <{Pipeline}>.", pipelineFileDirectory, Id);
        }
    }

    private readonly string pipelineFileDirectory;

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public LocalizedText DisplayName { get; }

    /// <inheritdoc/>
    public List<IPipelineStep> Steps { get; }

    /// <inheritdoc/>
    public ProcessingState State
    {
        get
        {
            var stepStates = this.Steps.Select(s => s.State).ToHashSet();

            if (stepStates.Count == 0)
            {
                return ProcessingState.Pending;
            }
            else if (stepStates.Contains(StepState.Error))
            {
                return ProcessingState.Failed;
            }
            else if (stepStates.Contains(StepState.Cancelled))
            {
                return ProcessingState.Cancelled;
            }
            else if (stepStates.Contains(StepState.Running))
            {
                return ProcessingState.Running;
            }
            else if (stepStates.Contains(StepState.DeliveryRestriction)
                && stepStates.All(s => s == StepState.Success || s == StepState.Skipped || s == StepState.Warning || s == StepState.DeliveryRestriction))
            {
                return ProcessingState.DeliveryRestriction;
            }
            else if (stepStates.Contains(StepState.Warning)
                && stepStates.All(s => s == StepState.Success || s == StepState.Skipped || s == StepState.Warning))
            {
                return ProcessingState.Warning;
            }
            else if (stepStates.All(s => s == StepState.Success || s == StepState.Skipped))
            {
                return ProcessingState.Success;
            }
            else if (stepStates.All(s => s == StepState.Pending))
            {
                return ProcessingState.Pending;
            }
            else
            {
                return ProcessingState.Running;
            }
        }
    }

    /// <inheritdoc/>
    public Guid JobId { get; }

    /// <inheritdoc/>
    public Func<IPipelineStep, StepResult, CancellationToken, Task>? OnStepCompleted { get; set; }

    private ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="id">The unique name of the pipeline.</param>
    /// <param name="displayName">The pipelines display name. A human-readable name for the pipeline.</param>
    /// <param name="steps">The steps in the pipeline.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="pipelineDirectory">The directory for the pipeline to use for storing temporary files. The pipeline is responsible for cleaning up the temporary files during dispose.</param>
    /// <param name="jobId">The job id associated with the pipeline execution, used for logging and tracking purposes.</param>
    private Pipeline(
        string id,
        LocalizedText displayName,
        List<IPipelineStep> steps,
        ILogger logger,
        string pipelineDirectory,
        Guid jobId)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Steps = steps;
        this.pipelineFileDirectory = pipelineDirectory;
        this.logger = logger;
        this.JobId = jobId;
    }

    /// <inheritdoc/>
    public async Task<PipelineContext> Run(IReadOnlyList<IPipelineFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);

        logger.LogInformation("starting pipeline");
        var context = new PipelineContext()
        {
            Upload = files,
            StepResults = new Dictionary<string, StepResult>(),
        };

        try
        {
            foreach (var step in this.Steps)
            {
                if (this.State == ProcessingState.Failed || this.State == ProcessingState.Cancelled)
                    break;

                var stepResult = await step.Run(context, cancellationToken).ConfigureAwait(false);
                context.StepResults[step.Id] = stepResult;

                if (this.OnStepCompleted is not null)
                    await this.OnStepCompleted(step, stepResult, cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation("all steps in pipeline executed");
            return context;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation (job timeout or host shutdown) is not a pipeline failure —
            // the currently running step marked itself Cancelled and the pipeline state
            // getter reports Cancelled, which is not deliverable.
            logger.LogInformation("pipeline cancelled");
            throw;
        }
    }

    internal static PipelineBuilder Builder() => new PipelineBuilder();

    internal class PipelineBuilder
    {
        private string? id;
        private LocalizedText? displayName;
        private List<IPipelineStep>? steps;
        private ILogger? logger;
        private string? pipelineDirectory;
        private Guid? jobId;

        public PipelineBuilder Id(string id)
        {
            this.id = id;
            return this;
        }

        public PipelineBuilder DisplayName(LocalizedText displayName)
        {
            this.displayName = displayName;
            return this;
        }

        public PipelineBuilder Steps(List<IPipelineStep>? steps)
        {
            this.steps = steps;
            return this;
        }

        public PipelineBuilder Logger(ILogger logger)
        {
            this.logger = logger;
            return this;
        }

        public PipelineBuilder PipelineDirectory(string pipelineDirectory)
        {
            this.pipelineDirectory = pipelineDirectory;
            return this;
        }

        public PipelineBuilder JobId(Guid jobId)
        {
            this.jobId = jobId;
            return this;
        }

        public Pipeline Build()
        {
            if (id == null)
                throw new InvalidOperationException("Pipeline Id must be provided.");
            if (displayName == null)
                throw new InvalidOperationException("Pipeline DisplayName must be provided.");
            if (steps == null)
                throw new InvalidOperationException("Pipeline Steps must be provided.");
            if (logger == null)
                throw new InvalidOperationException("Logger must be provided.");
            if (pipelineDirectory == null)
                throw new InvalidOperationException("Pipeline Directory must be provided.");
            if (jobId == null)
                throw new InvalidOperationException("Pipeline JobId must be provided.");

            return new Pipeline(id, displayName, steps, logger, pipelineDirectory, jobId.Value);
        }
    }
}
