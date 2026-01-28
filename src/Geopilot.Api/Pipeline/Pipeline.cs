using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a sequence of processing steps and associated parameters to be executed as a pipeline.
/// </summary>
/// <remarks>A pipeline consists of an ordered collection of steps, each of which performs a specific operation.
/// Optionally, parameters can be provided to configure the behavior of the pipeline or its steps.</remarks>
internal class Pipeline : IPipeline
{
    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public Dictionary<string, string> DisplayName { get; set; }

    /// <inheritdoc/>
    public PipelineParametersConfig Parameters { get; }

    /// <inheritdoc/>
    public List<PipelineStep> Steps { get; }

    /// <inheritdoc/>
    public PipelineState State { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="id">The unique name of the pipeline.</param>
    /// <param name="displayName">The pipelines display name. A human-readable name for the pipeline.</param>
    /// <param name="steps">The steps in the pipeline.</param>
    /// <param name="parameters">The parameters for the pipeline.</param>
    public Pipeline(string id, Dictionary<string, string> displayName, List<PipelineStep> steps, PipelineParametersConfig parameters)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Steps = steps;
        this.Parameters = parameters;
        this.State = PipelineState.Pending;
    }

    /// <inheritdoc/>
    public PipelineContext Run(FileHandle file)
    {
        this.State = PipelineState.Running;

        var context = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>(),
        };

        var uploadStepResult = CreateUploadStepResult(file);
        context.StepResults[this.Parameters.UploadStep] = uploadStepResult;

        foreach (var step in this.Steps)
        {
            var stepResult = step.Run(context);
            context.StepResults[step.Id] = stepResult;
        }

        this.State = PipelineState.Success;

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
                    Action = OutputAction.IGNORE,
                    Data = file,
                };
                stepResult.Outputs[mapping.Attribute] = output;
                break;
            }
        }

        return stepResult;
    }
}
