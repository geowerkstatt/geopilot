using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Threading.Tasks;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
public interface IPipelineStep : IDisposable
{
    /// <summary>
    /// The name of the step. This name is unique within the pipeline. Other Steps reference this name to define data flow.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// A human-readable display name for the step in different languages. Key: ISO 639 language code, Value: The display name for that language.
    /// </summary>
    Dictionary<string, string> DisplayName { get; }

    /// <summary>
    /// The input configuration for this step.
    /// </summary>
    List<InputConfig> InputConfig { get; }

    /// <summary>
    /// The output configuration for this step.
    /// </summary>
    List<OutputConfig> OutputConfigs { get; }

    List<StepConditionConfig> StepConditions { get; }

    /// <summary>
    /// The process to be executed for this step.
    /// </summary>
    object Process { get; }

    /// <summary>
    /// The current state of the step.
    /// </summary>
    StepState State { get; set; }

    /// <summary>
    /// Runs the step with the given context.
    /// </summary>
    /// <param name="context">Context with the aggregated step results from previous steps.</param>
    /// <param name="cancellationToken">Cancellation token to cancle the pipeline run.</param>
    /// <returns>The output data from the step.</returns>
    Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken);
}
