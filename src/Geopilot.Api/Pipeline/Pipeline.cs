using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a sequence of processing steps and associated parameters to be executed as a pipeline.
/// </summary>
/// <remarks>A pipeline consists of an ordered collection of steps, each of which performs a specific operation.
/// Optionally, parameters can be provided to configure the behavior of the pipeline or its steps.</remarks>
public sealed class Pipeline : IPipeline
{
    /// <inheritdoc/>
    public void Dispose()
    {
        Steps.ForEach(step => step.Dispose());

        if (Path.Exists(pipelineFileDirectory))
            Directory.Delete(pipelineFileDirectory, true);
    }

    private readonly ConditionEvaluator conditionEvaluator;
    private readonly string pipelineFileDirectory;

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public Dictionary<string, string> DisplayName { get; }

    /// <inheritdoc/>
    public PipelineParametersConfig Parameters { get; }

    private string? deliveryCondition;

    /// <inheritdoc/>
    public List<IPipelineStep> Steps { get; }

    /// <inheritdoc/>
    public PipelineState State
    {
        get
        {
            var stepStates = this.Steps.Select(s => s.State).ToHashSet();

            if (stepStates.Count == 0)
            {
                return PipelineState.Pending;
            }
            else if (stepStates.Contains(StepState.Error))
            {
                return PipelineState.Failed;
            }
            else if (stepStates.Contains(StepState.Running))
            {
                return PipelineState.Running;
            }
            else if (stepStates.All(s => s == StepState.Success || s == StepState.Skipped))
            {
                return PipelineState.Success;
            }
            else if (stepStates.All(s => s == StepState.Pending))
            {
                return PipelineState.Pending;
            }
            else
            {
                return PipelineState.Running;
            }
        }
    }

    /// <inheritdoc/>
    public Guid JobId { get; }

    /// <inheritdoc/>
    public PipelineDelivery Delivery { get; set; } = PipelineDelivery.Allow;

    /// <summary>
    /// The file to be processed by the pipeline.
    /// </summary>
    private readonly IPipelineFile file;

    private ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="id">The unique name of the pipeline.</param>
    /// <param name="displayName">The pipelines display name. A human-readable name for the pipeline.</param>
    /// <param name="steps">The steps in the pipeline.</param>
    /// <param name="parameters">The parameters for the pipeline.</param>
    /// <param name="deliveryCondition">Expression to determine when the pipeline step data can be delivered.</param>
    /// <param name="file">The file to be processed by the pipeline.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="pipelineDirectory">The directory for the pipeline to use for storing temporary files. The pipeline is responsible for cleaning up the temporary files during dispose.</param>
    /// <param name="jobId">The job id associated with the pipeline execution, used for logging and tracking purposes.</param>
    private Pipeline(
        string id,
        Dictionary<string, string> displayName,
        List<IPipelineStep> steps,
        PipelineParametersConfig parameters,
        string? deliveryCondition,
        IPipelineFile file,
        ILogger logger,
        string pipelineDirectory,
        Guid jobId)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Steps = steps;
        this.Parameters = parameters;
        this.deliveryCondition = deliveryCondition;
        this.file = file;
        this.conditionEvaluator = new ConditionEvaluator(logger);
        this.pipelineFileDirectory = pipelineDirectory;
        this.logger = logger;
        this.JobId = jobId;
    }

    /// <inheritdoc/>
    public async Task<PipelineContext> Run(CancellationToken cancellationToken)
    {
        logger.LogInformation($"starting pipeline");
        var context = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>(),
        };

        if (file != null)
        {
            var uploadStepResult = CreateUploadStepResult(file);
            context.StepResults[this.Parameters.UploadStep] = uploadStepResult;
        }

        foreach (var step in this.Steps)
        {
            if (this.State == PipelineState.Failed)
                break;
            var stepResult = await step.Run(context, cancellationToken).ConfigureAwait(false);
            context.StepResults[step.Id] = stepResult;
        }

        await this.EvaluateDeliveryCondition(context);

        logger.LogInformation($"all steps in pipeline executed");
        return context;
    }

    private async Task EvaluateDeliveryCondition(PipelineContext context)
    {
        if (!string.IsNullOrEmpty(this.deliveryCondition))
        {
            var expressionParameters = context.ToExpressionParameters();
            var allowDelivery = await this.conditionEvaluator.EvaluateConditionAsync(this.deliveryCondition, expressionParameters);
            if (allowDelivery)
                this.Delivery = PipelineDelivery.Allow;
            else
                this.Delivery = PipelineDelivery.Prevent;
        }
        else
        {
            this.Delivery = PipelineDelivery.Allow;
        }
    }

    private StepResult CreateUploadStepResult(IPipelineFile file)
    {
        var stepResult = new StepResult();

        var fileExtension = file.FileExtension;
        foreach (var mapping in this.Parameters.Mappings)
        {
            if (string.Equals(fileExtension, mapping.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                var output = new StepOutput()
                {
                    Action = new HashSet<OutputAction>(),
                    Data = file,
                };
                stepResult.Outputs[mapping.Attribute] = output;
                break;
            }
        }

        return stepResult;
    }

    internal static PipelineBuilder Builder() => new PipelineBuilder();

    internal class PipelineBuilder
    {
        private string? id;
        private Dictionary<string, string>? displayName;
        private List<IPipelineStep>? steps;
        private PipelineParametersConfig? parameters;
        private string? deliveryCondition;
        private IPipelineFile? file;
        private ILogger? logger;
        private string? pipelineDirectory;
        private Guid? jobId;

        public PipelineBuilder Id(string id)
        {
            this.id = id;
            return this;
        }

        public PipelineBuilder DisplayName(Dictionary<string, string> displayName)
        {
            this.displayName = displayName;
            return this;
        }

        public PipelineBuilder Steps(List<IPipelineStep>? steps)
        {
            this.steps = steps;
            return this;
        }

        public PipelineBuilder Parameters(PipelineParametersConfig parameters)
        {
            this.parameters = parameters;
            return this;
        }

        public PipelineBuilder DeliveryCondition(string? deliveryCondition)
        {
            this.deliveryCondition = deliveryCondition;
            return this;
        }

        public PipelineBuilder File(IPipelineFile file)
        {
            this.file = file;
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
            if (parameters == null)
                throw new InvalidOperationException("Pipeline Parameters must be provided.");
            if (file == null)
                throw new InvalidOperationException("Pipeline File must be provided.");
            if (logger == null)
                throw new InvalidOperationException("Logger must be provided.");
            if (pipelineDirectory == null)
                throw new InvalidOperationException("Pipeline Directory must be provided.");
            if (jobId == null)
                throw new InvalidOperationException("Pipeline JobId must be provided.");

            return new Pipeline(id, displayName, steps, parameters, deliveryCondition, file, logger, pipelineDirectory, jobId.Value);
        }
    }
}
