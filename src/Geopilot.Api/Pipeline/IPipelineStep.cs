using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Pipeline;

internal interface IPipelineStep
{
    /// <summary>
    /// The name of the step. This name is unique within the pipeline. Other Steps reference this name to define data flow.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The steps display name. A human-readable name for the step.
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

    /// <summary>
    /// The process to be executed for this step.
    /// </summary>
    IPipelineProcess Process { get; }

    /// <summary>
    /// The current state of the step.
    /// </summary>
    StepState State { get; set; }

    /// <summary>
    /// Runs the step with the given context.
    /// </summary>
    /// <param name="context">Context with the aggregated step results from previous steps.</param>
    /// <returns>The output data from the step.</returns>
    StepResult Run(PipelineContext context);
}
