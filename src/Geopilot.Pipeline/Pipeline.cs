using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline;

/// <summary>
/// Represents a sequence of processing steps and associated parameters to be executed as a pipeline.
/// </summary>
/// <remarks>A pipeline consists of an ordered collection of steps, each of which performs a specific operation.
/// Optionally, parameters can be provided to configure the behavior of the pipeline or its steps.</remarks>
public sealed class Pipeline : IPipeline
{
    private bool disposed;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;

        Steps.ForEach(step => step.Dispose());

        if (Path.Exists(pipelineFileDirectory))
            Directory.Delete(pipelineFileDirectory, true);

        disposed = true;
    }

    private readonly ConditionEvaluator conditionEvaluator;
    private readonly string pipelineFileDirectory;

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public LocalizedText DisplayName { get; }

    private List<ConditionConfig>? deliveryRestrictions;

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

    /// <inheritdoc/>
    public PipelineDelivery Delivery { get; set; } = PipelineDelivery.Allow;

    /// <inheritdoc/>
    public LocalizedText? DeliveryRestrictionMessage { get; private set; }

    private ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="id">The unique name of the pipeline.</param>
    /// <param name="displayName">The pipelines display name. A human-readable name for the pipeline.</param>
    /// <param name="steps">The steps in the pipeline.</param>
    /// <param name="deliveryRestrictions">Restrictions to determine when the pipeline data can not be delivered. If any restriction evaluates to true, delivery is prevented.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="pipelineDirectory">The directory for the pipeline to use for storing temporary files. The pipeline is responsible for cleaning up the temporary files during dispose.</param>
    /// <param name="jobId">The job id associated with the pipeline execution, used for logging and tracking purposes.</param>
    private Pipeline(
        string id,
        LocalizedText displayName,
        List<IPipelineStep> steps,
        List<ConditionConfig>? deliveryRestrictions,
        ILogger logger,
        string pipelineDirectory,
        Guid jobId)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Steps = steps;
        this.deliveryRestrictions = deliveryRestrictions;
        this.conditionEvaluator = new ConditionEvaluator(logger);
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

            await this.EvaluateDeliveryCondition(context);

            logger.LogInformation("all steps in pipeline executed");
            return context;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation (job timeout or host shutdown) is not a pipeline failure —
            // the currently running step marked itself Cancelled, the pipeline state
            // getter reports Cancelled, and we prevent delivery defensively before
            // rethrowing so the caller can distinguish timeout from crash.
            this.Delivery = PipelineDelivery.Prevent;
            logger.LogInformation("pipeline cancelled");
            throw;
        }
    }

    private async Task EvaluateDeliveryCondition(PipelineContext context)
    {
        if (this.State == ProcessingState.Failed)
        {
            this.Delivery = PipelineDelivery.Prevent;
        }
        else
        {
            if (this.deliveryRestrictions != null && this.deliveryRestrictions.Count > 0)
            {
                var matchedRestrictions = await FindMatchingRestrictions(context);
                if (matchedRestrictions.Count > 0)
                {
                    this.Delivery = PipelineDelivery.Prevent;
                    AddRestrictionMessages(context, matchedRestrictions);
                }
                else
                {
                    this.Delivery = PipelineDelivery.Allow;
                }
            }
            else
            {
                this.Delivery = PipelineDelivery.Allow;
            }
        }
    }

    private async Task<List<ConditionConfig>> FindMatchingRestrictions(PipelineContext context)
    {
        var matched = new List<ConditionConfig>();
        var expressionParameters = context.ToExpressionParameters();
        foreach (var restriction in this.deliveryRestrictions!)
        {
            if (await this.conditionEvaluator.EvaluateConditionAsync(restriction.Expression, expressionParameters))
                matched.Add(restriction);
        }

        return matched;
    }

    private void AddRestrictionMessages(PipelineContext context, List<ConditionConfig> matchedRestrictions)
    {
        var mergedMessages = MergeConditionMessages(matchedRestrictions);
        if (mergedMessages.Count > 0)
        {
            context.DeliveryRestrictionMessage = mergedMessages;
            this.DeliveryRestrictionMessage = mergedMessages;
        }
    }

    private static LocalizedText MergeConditionMessages(List<ConditionConfig> conditions) =>
        LocalizedText.Merge(
            conditions.Where(c => c.Message is not null).Select(c => c.Message!),
            ", ");

    internal static PipelineBuilder Builder() => new PipelineBuilder();

    internal class PipelineBuilder
    {
        private string? id;
        private LocalizedText? displayName;
        private List<IPipelineStep>? steps;
        private List<ConditionConfig>? deliveryRestrictions;
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

        public PipelineBuilder DeliveryRestrictions(List<ConditionConfig>? deliveryRestrictions)
        {
            this.deliveryRestrictions = deliveryRestrictions;
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

            return new Pipeline(id, displayName, steps, deliveryRestrictions, logger, pipelineDirectory, jobId.Value);
        }
    }
}
