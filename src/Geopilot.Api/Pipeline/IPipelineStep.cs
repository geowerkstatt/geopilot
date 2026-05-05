using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;

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

    /// <summary>
    /// Gets the configuration settings that define the conditions under which the pipeline step is executed, skiped or failed.
    /// </summary>
    PipelineStepConditionsConfig? StepConditions { get; }

    /// <summary>
    /// The process to be executed for this step.
    /// </summary>
    object Process { get; }

    /// <summary>
    /// The current state of the step.
    /// </summary>
    StepState State { get; set; }

    /// <summary>
    /// The result produced by the step's process. Null until the step has run (or was skipped before producing a result).
    /// </summary>
    StepResult? Result { get; }

    /// <summary>
    /// Files produced by the step that have been persisted to disk for download (or delivery).
    /// Populated by the processing runner after the step completes for outputs configured with
    /// <see cref="OutputAction.Download"/> or <see cref="OutputAction.Delivery"/>. Order matches
    /// the order of the step's output configs.
    /// </summary>
    IList<PersistedDownload> PersistedDownloads { get; }

    /// <summary>
    /// Runs the step with the given context.
    /// </summary>
    /// <param name="context">Context with the aggregated step results from previous steps.</param>
    /// <param name="cancellationToken">Cancellation token to cancle the pipeline run.</param>
    /// <returns>The output data from the step.</returns>
    Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken);
}
