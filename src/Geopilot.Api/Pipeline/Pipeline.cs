using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a sequence of processing steps and associated parameters to be executed as a pipeline.
/// </summary>
/// <remarks>A pipeline consists of an ordered collection of steps, each of which performs a specific operation.
/// Optionally, parameters can be provided to configure the behavior of the pipeline or its steps.</remarks>
public class Pipeline : IPipeline
{
    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public Dictionary<string, string> DisplayName { get; }

    /// <inheritdoc/>
    public PipelineParametersConfig Parameters { get; }

    /// <inheritdoc/>
    public List<IPipelineStep> Steps { get; }

    /// <inheritdoc/>
    public PipelineState State
    {
        get
        {
            var worstStepState = WorstStepState(this.Steps.Select(s => s.State));
            switch (worstStepState)
            {
                case StepState.Pending:
                    return PipelineState.Pending;
                case StepState.Running:
                    return PipelineState.Running;
                case StepState.Success:
                    return PipelineState.Success;
                case StepState.Failed:
                    return PipelineState.Failed;
                default:
                    return PipelineState.Pending;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="id">The unique name of the pipeline.</param>
    /// <param name="displayName">The pipelines display name. A human-readable name for the pipeline.</param>
    /// <param name="steps">The steps in the pipeline.</param>
    /// <param name="parameters">The parameters for the pipeline.</param>
    public Pipeline(string id, Dictionary<string, string> displayName, List<IPipelineStep> steps, PipelineParametersConfig parameters)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Steps = steps;
        this.Parameters = parameters;
    }

    /// <inheritdoc/>
    public PipelineContext Run(FileHandle file)
    {
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
            var stepResult = step.Run(context);
            if (stepResult != null)
                context.StepResults[step.Id] = stepResult;
        }

        return context;
    }

    private StepResult CreateUploadStepResult(FileHandle file)
    {
        var stepResult = new StepResult();

        var fileExtension = Path.GetExtension(file.FileName).TrimStart('.');
        foreach (var mapping in this.Parameters.Mappings)
        {
            if (string.Equals(fileExtension, mapping.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                var output = new StepOutput()
                {
                    Action = OutputAction.Ignore,
                    Data = file,
                };
                stepResult.Outputs[mapping.Attribute] = output;
                break;
            }
        }

        return stepResult;
    }

    /// <summary>
    /// Determines the worst state among a collection of step states.
    /// </summary>
    /// <param name="states">The collection of step states.</param>
    /// <returns>The worst step state.</returns>
    private static StepState WorstStepState(IEnumerable<StepState> states)
    {
        if (states == null || !states.Any())
            return StepState.Pending;

        return states.Max();
    }
}
