using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

/// <summary>
/// Interface for a pipeline.
/// </summary>
public interface IPipeline : IDisposable
{
    /// <summary>
    /// The unique id of the pipeline.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The pipeline's localized display name.
    /// </summary>
    LocalizedText DisplayName { get; }

    /// <summary>
    /// The steps in the pipeline to be executed sequentially.
    /// </summary>
    List<IPipelineStep> Steps { get; }

    /// <summary>
    /// The current state of the pipeline.
    /// </summary>
    ProcessingState State { get; }

    /// <summary>
    /// The unique identifier for the job associated with this pipeline execution, used for logging and tracking purposes.
    /// </summary>
    Guid JobId { get; }

    /// <summary>
    /// Optional callback invoked by <see cref="Run"/> right before each step runs, in step order, with the step
    /// about to run. Awaited before the step starts, so a handler can reliably record that the pipeline reached
    /// the step. The step's own <see cref="IPipelineStep.State"/> still reads <see cref="StepState.Pending"/> at
    /// this point; it transitions inside the step's run, after its pre-conditions were evaluated.
    /// <see langword="null"/> (the default) disables the callback.
    /// </summary>
    Func<IPipelineStep, CancellationToken, Task>? OnStepStarted { get; set; }

    /// <summary>
    /// Optional callback invoked by <see cref="Run"/> after each step has finished, in step order, with the
    /// just-completed step and its <see cref="StepResult"/>. Awaited before the next step runs, so a handler can
    /// reliably react to a step (e.g. persist its outputs) before the pipeline progresses. <see langword="null"/>
    /// (the default) disables the callback.
    /// </summary>
    Func<IPipelineStep, StepResult, CancellationToken, Task>? OnStepCompleted { get; set; }

    /// <summary>
    /// Runs the pipeline with the specified input files.
    /// </summary>
    /// <param name="files">The files to be processed by the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pipeline run.</param>
    /// <returns>The context containing the results of the pipeline execution.</returns>
    /// <exception cref="PipelineRunException">The pipeline run failed due to a misconfigured pipeline or an misbehaving process.</exception>
    /// <exception cref="OperationCanceledException">The pipeline run was cancelled.</exception>
    Task<PipelineContext> Run(IReadOnlyList<IPipelineFile> files, CancellationToken cancellationToken);
}
