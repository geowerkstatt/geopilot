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
    /// The output actions for this step: which result properties are tagged with which actions.
    /// </summary>
    IReadOnlyList<OutputActionConfig> OutputActions { get; }

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
    /// When the step started running (UTC), before its pre-conditions were evaluated.
    /// <see langword="null"/> until the pipeline reaches the step.
    /// </summary>
    DateTime? StartedAt { get; }

    /// <summary>
    /// When the step finished (UTC), whatever its terminal state, including skipped and cancelled.
    /// <see langword="null"/> while the step is pending or running.
    /// </summary>
    DateTime? FinishedAt { get; }

    /// <summary>
    /// The message of the exception that failed or cancelled this step. <see langword="null"/> when the step
    /// did not throw; a step failed by a pre- or post-condition carries its reason in
    /// <see cref="ConditionMessage"/> instead.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// The evaluation result of every condition checked for this step, in evaluation order: pre-conditions
    /// (fail, then skip), then post-conditions (fail, then restrict-delivery, then warn). Contains
    /// non-matching evaluations too, so a consumer can tell "checked and did not apply" apart from
    /// "never checked". Empty until the step ran.
    /// </summary>
    IReadOnlyList<ConditionEvaluation> ConditionEvaluations { get; }

    /// <summary>
    /// The localized status message produced by the process itself (outputs tagged with
    /// <see cref="OutputAction.StatusMessage"/>). <see langword="null"/> if the step has not run, or
    /// ran without emitting any status message. Condition-driven messages are exposed separately via
    /// <see cref="ConditionMessage"/>.
    /// </summary>
    LocalizedText? StatusMessage { get; }

    /// <summary>
    /// The localized message from the step conditions that determined the terminal state (pre-fail,
    /// pre-skip, post-fail or post-warn). Set during <see cref="Run"/> when a matching condition carries
    /// a message. <see langword="null"/> when no condition matched or the matching conditions had no message.
    /// </summary>
    LocalizedText? ConditionMessage { get; }

    /// <summary>
    /// Files produced by the step that are available for the user to download (outputs configured
    /// with <see cref="OutputAction.Download"/>). Populated by the processing runner (via
    /// <see cref="AddDownload"/>) as soon as the step completes, so they can be offered while later
    /// steps still run. Order matches the order of the step's output configs. Read-only; append via
    /// <see cref="AddDownload"/>.
    /// </summary>
    IReadOnlyList<PersistedFile> Downloads { get; }

    /// <summary>
    /// Files produced by the step that should be included in the delivery (outputs configured with
    /// <see cref="OutputAction.Delivery"/>). Populated by the processing runner (via
    /// <see cref="AddDeliveryFile"/>) only after a successful, deliverable run. A file tagged with both
    /// <see cref="OutputAction.Download"/> and <see cref="OutputAction.Delivery"/> appears in both lists.
    /// Read-only; append via <see cref="AddDeliveryFile"/>.
    /// </summary>
    IReadOnlyList<PersistedFile> DeliveryFiles { get; }

    /// <summary>
    /// Appends a file to <see cref="Downloads"/>. Safe to call while another thread reads
    /// <see cref="Downloads"/>; readers observe a consistent snapshot.
    /// </summary>
    void AddDownload(PersistedFile file);

    /// <summary>
    /// Appends a file to <see cref="DeliveryFiles"/>. Safe to call while another thread reads
    /// <see cref="DeliveryFiles"/>; readers observe a consistent snapshot.
    /// </summary>
    void AddDeliveryFile(PersistedFile file);

    /// <summary>
    /// Visualization configs produced by the step (outputs configured with
    /// <see cref="OutputAction.Visualization"/>). Populated by the processing runner (via
    /// <see cref="AddVisualization"/>) as soon as the step completes. The config object is serialized
    /// to JSON in the dedicated visualization store and served from the visualization endpoint;
    /// it is not a download. Read-only; append via <see cref="AddVisualization"/>.
    /// </summary>
    IReadOnlyList<StepVisualization> Visualizations { get; }

    /// <summary>
    /// Appends a visualization to <see cref="Visualizations"/>. Safe to call while another thread
    /// reads <see cref="Visualizations"/>; readers observe a consistent snapshot.
    /// </summary>
    void AddVisualization(StepVisualization visualization);

    /// <summary>
    /// Runs the step with the given context.
    /// </summary>
    /// <param name="context">Context with the aggregated step results from previous steps.</param>
    /// <param name="cancellationToken">Cancellation token to cancle the pipeline run.</param>
    /// <returns>The output data from the step.</returns>
    Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken);
}
