using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// What became of a pipeline run.
/// </summary>
public enum PipelineRunOutcomeKind
{
    /// <summary>
    /// A delivery was declared for the run.
    /// </summary>
    Delivered,

    /// <summary>
    /// A delivery was declared for the run and deleted afterwards.
    /// </summary>
    DeliveredAndDeleted,

    /// <summary>
    /// The run allowed a delivery, but none was declared.
    /// </summary>
    NotDelivered,

    /// <summary>
    /// The run did not allow a delivery.
    /// </summary>
    NotDeliverable,

    /// <summary>
    /// The run allowed a delivery, but whoever started it may not deliver.
    /// </summary>
    NotDeclarable,

    /// <summary>
    /// The run allowed a delivery and was to be delivered on its client's behalf, but the declaration failed.
    /// </summary>
    DeclarationFailed,

    /// <summary>
    /// The job of the run is still running.
    /// </summary>
    Running,

    /// <summary>
    /// The run has no terminal state and its job is gone: its instance stopped while it ran.
    /// </summary>
    Unknown,
}

/// <summary>
/// What became of a pipeline run, derived from its protocol, its delivery and whether its job still runs.
/// </summary>
/// <param name="Kind">What became of the run.</param>
/// <param name="DeliveryId">The delivery declared for the run, deleted or not.</param>
/// <param name="BlockingSteps">For a run that did not allow a delivery: every step that restricted the delivery, failed or was cancelled, in pipeline order. A restriction does not stop the pipeline, so there can be several; none when the run failed outside the steps.</param>
/// <param name="PreflightFailed">Whether the run failed in the preflight, before any step started.</param>
public record PipelineRunOutcome(PipelineRunOutcomeKind Kind, int? DeliveryId, List<PipelineRunBlockingStep> BlockingSteps, bool PreflightFailed);

/// <summary>
/// A step that kept a pipeline run from allowing a delivery.
/// </summary>
/// <param name="StepId">The step id from the pipeline definition.</param>
/// <param name="DisplayName">The step's localized display name.</param>
public record PipelineRunBlockingStep(string StepId, LocalizedText DisplayName);
