using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Models;

/// <summary>
/// The protocol record of one pipeline step within a <see cref="PipelineRun"/>. Written when the pipeline
/// reaches the step (state <see cref="StepState.Running"/>) and updated once with its terminal state, so a
/// run interrupted by a restart shows which step was in flight.
/// </summary>
public class PipelineRunStep
{
    /// <summary>
    /// The unique identifier for the step record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The run this step belongs to.
    /// </summary>
    public int PipelineRunId { get; set; }

    /// <summary>
    /// Navigation to the run.
    /// </summary>
    public PipelineRun PipelineRun { get; set; } = null!;

    /// <summary>
    /// The position of the step within the pipeline, starting at 0.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The step id from the pipeline definition. Unique within the run.
    /// </summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>
    /// The step's localized display name. Also part of the run's definition snapshot, but repeated here
    /// so a reader understands the row without JSON navigation.
    /// </summary>
    public LocalizedText DisplayName { get; set; } = LocalizedText.Empty;

    /// <summary>
    /// The fully qualified type name of the process implementation that ran.
    /// </summary>
    public string ProcessImplementation { get; set; } = string.Empty;

    /// <summary>
    /// The simple name of the assembly the process implementation came from. Together with
    /// <see cref="ProcessAssemblyVersion"/> this pins the build that ran, which the definition snapshot
    /// cannot (it only names the type).
    /// </summary>
    public string? ProcessAssemblyName { get; set; }

    /// <summary>
    /// The version of the assembly the process implementation came from.
    /// </summary>
    public string? ProcessAssemblyVersion { get; set; }

    /// <summary>
    /// The state of the step: <see cref="StepState.Running"/> while it runs, its terminal state afterwards.
    /// A row stuck in <see cref="StepState.Running"/> on a run without terminal state marks the step the
    /// instance died in.
    /// </summary>
    public StepState State { get; set; }

    /// <summary>
    /// When the pipeline reached the step (UTC), before its pre-conditions were evaluated.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the step finished (UTC), whatever its terminal state.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// The message of the exception that failed or cancelled the step. A step failed by a condition
    /// carries its reason in <see cref="ConditionMessage"/> and in <see cref="Conditions"/> instead.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The localized status message the process emitted, if any.
    /// </summary>
    public LocalizedText? StatusMessage { get; set; }

    /// <summary>
    /// The localized message of the conditions that determined the step's terminal state, if any.
    /// </summary>
    public LocalizedText? ConditionMessage { get; set; }

    /// <summary>
    /// The evaluation result of every condition checked for this step, matching or not.
    /// </summary>
    public List<PipelineRunCondition> Conditions { get; set; } = new();

    /// <summary>
    /// The artifacts the step produced (downloads, visualizations, delivery files), by name.
    /// </summary>
    public List<PipelineRunArtifact> Artifacts { get; set; } = new();
}
