using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;
using System.Linq;

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
    }

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
            var stepStates = this.Steps.Select(s => s.State).ToHashSet();

            if (stepStates.Count == 0)
            {
                return PipelineState.Pending;
            }
            else if (stepStates.Contains(StepState.Failed))
            {
                return PipelineState.Failed;
            }
            else if (stepStates.Contains(StepState.Running))
            {
                return PipelineState.Running;
            }
            else if (stepStates.Contains(StepState.SuccessWithWarnings))
            {
                return PipelineState.SuccessWithWarnings;
            }
            else if (stepStates.All(s => s == StepState.Success))
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
    public async Task<PipelineContext> Run(IPipelineTransferFile file, CancellationToken cancellationToken)
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
            var stepResult = await step.Run(context, cancellationToken).ConfigureAwait(false);
            context.StepResults[step.Id] = stepResult;
        }

        return context;
    }

    private StepResult CreateUploadStepResult(IPipelineTransferFile file)
    {
        var stepResult = new StepResult();

        var fileExtension = Path.GetExtension(file.FileName).TrimStart('.');
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
}
