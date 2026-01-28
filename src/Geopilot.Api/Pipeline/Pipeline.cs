using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;
using NetTopologySuite.Index.HPRtree;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a sequence of processing steps and associated parameters to be executed as a pipeline.
/// </summary>
/// <remarks>A pipeline consists of an ordered collection of steps, each of which performs a specific operation.
/// Optionally, parameters can be provided to configure the behavior of the pipeline or its steps.</remarks>
internal class Pipeline
{
    /// <summary>
    /// The unique name of the pipeline.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The parameters for the pipeline.
    /// </summary>
    public PipelineParametersConfig Parameters { get; }

    /// <summary>
    /// The steps in the pipeline to be executed sequentially.
    /// </summary>
    public List<PipelineStep> Steps { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="name">The unique name of the pipeline.</param>
    /// <param name="steps">The steps in the pipeline.</param>
    /// <param name="parameters">The parameters for the pipeline.</param>
    public Pipeline(string name, List<PipelineStep> steps, PipelineParametersConfig parameters)
    {
        this.Name = name;
        this.Steps = steps;
        this.Parameters = parameters;
    }

    public PipelineContext Run(FileHandle file)
    {
        var context = new PipelineContext()
        {
            State = PipelineState.Running,
            StepResults = new Dictionary<string, StepResult>(),
        };

        var uploadStepResult = CreateUploadStepResult(file);
        context.StepResults[this.Parameters.UploadStep] = uploadStepResult;

        foreach (var step in this.Steps)
        {
            var stepResult = step.Run(context);
            context.StepResults[step.Name] = stepResult;
        }

        return context;
    }

    private StepResult CreateUploadStepResult(FileHandle file)
    {
        var stepResult = new StepResult() { State = StepState.Success };

        var fileExtension = Path.GetExtension(file.FileName).TrimStart('.');
        foreach (var mapping in this.Parameters.Mapping)
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
