using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

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
    /// The step's localized display name.
    /// </summary>
    LocalizedText DisplayName { get; }

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
    /// The localized status message produced by the step. Merges all outputs tagged with
    /// <see cref="OutputAction.StatusMessage"/>, including condition-driven pre-fail, pre-skip,
    /// and post-fail messages added during <see cref="Run"/>. <see langword="null"/> if the step
    /// has not run, or ran without emitting any status message.
    /// </summary>
    LocalizedText? StatusMessage { get; }

    /// <summary>
    /// Files produced by the step that are available for the user to download (outputs
    /// configured with <see cref="OutputAction.Download"/>). Populated by the processing runner
    /// after the step completes. Order matches the order of the step's output configs.
    /// </summary>
    IList<PersistedFile> Downloads { get; }

    /// <summary>
    /// Files produced by the step that should be included in the delivery (outputs configured
    /// with <see cref="OutputAction.Delivery"/>). Populated by the processing runner after the
    /// step completes. A file tagged with both <see cref="OutputAction.Download"/> and
    /// <see cref="OutputAction.Delivery"/> appears in both lists and is persisted only once.
    /// </summary>
    IList<PersistedFile> DeliveryFiles { get; }

    /// <summary>
    /// Runs the step with the given context.
    /// </summary>
    /// <param name="context">Context with the aggregated step results from previous steps.</param>
    /// <param name="cancellationToken">Cancellation token to cancle the pipeline run.</param>
    /// <returns>The output data from the step.</returns>
    Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken);
}
