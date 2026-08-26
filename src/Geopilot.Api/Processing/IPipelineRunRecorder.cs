using Geopilot.Api.Models;
using Geopilot.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// Writes the execution protocol (<see cref="PipelineRun"/> and children) while a job runs.
///
/// Two write policies on purpose: <see cref="RecordJobStartedAsync"/> is hard and throws when the record
/// cannot be written, because a job we cannot account for must not be accepted. Every later method is
/// soft: it catches all its own errors and logs a warning, because a protocol failure must not tear down
/// a running pipeline. A run whose terminal write failed keeps a null terminal state and reads as
/// "outcome unknown", indistinguishable from a restart victim, which is the honest answer.
/// </summary>
public interface IPipelineRunRecorder
{
    /// <summary>
    /// Writes the run record including the definition snapshot and the upload manifest. Hard: an exception
    /// here means no record, and the caller must not start the job.
    /// </summary>
    /// <param name="job">The created job with its pipeline attached.</param>
    /// <param name="mandate">The mandate the job was started for.</param>
    /// <param name="user">The user that started the job, or <see langword="null"/> for an anonymous delivery.</param>
    /// <param name="upload">The upload whose files the job processes.</param>
    Task RecordJobStartedAsync(ProcessingJob job, Mandate mandate, User? user, UploadInfo upload);

    /// <summary>
    /// Records the outcome of the malware scan and the per-file hashes it computed. Soft.
    /// </summary>
    /// <param name="jobId">The job whose upload was scanned.</param>
    /// <param name="scanResult">The scan outcome.</param>
    Task RecordScanOutcomeAsync(Guid jobId, Services.ScanResult scanResult);

    /// <summary>
    /// Marks the run as failed before its pipeline ever ran (preflight failure). Soft.
    /// </summary>
    /// <param name="jobId">The job whose preflight failed.</param>
    /// <param name="failureReason">Why the preflight failed.</param>
    Task RecordPreflightFailedAsync(Guid jobId, string failureReason);

    /// <summary>
    /// Writes the step row the moment the pipeline reaches the step, so an interrupted run shows which
    /// step was in flight. Soft.
    /// </summary>
    /// <param name="jobId">The job the step belongs to.</param>
    /// <param name="pipelineStep">The step about to run.</param>
    /// <param name="order">The position of the step within the pipeline, starting at 0.</param>
    Task RecordStepStartedAsync(Guid jobId, IPipelineStep pipelineStep, int order);

    /// <summary>
    /// Updates the step row with its terminal state, timing, messages, condition evaluations and the
    /// artifacts persisted so far. Soft.
    /// </summary>
    /// <param name="jobId">The job the step belongs to.</param>
    /// <param name="pipelineStep">The completed step.</param>
    /// <param name="order">The position of the step within the pipeline, starting at 0.</param>
    Task RecordStepCompletedAsync(Guid jobId, IPipelineStep pipelineStep, int order);

    /// <summary>
    /// Writes the run's terminal state and reconciles every step row: steps that threw or were never
    /// reached get their row here, and delivery artifacts extracted after the last step are added. Soft.
    /// </summary>
    /// <param name="jobId">The finished job.</param>
    /// <param name="steps">All steps of the pipeline, in pipeline order.</param>
    /// <param name="terminalState">The state the job ended in.</param>
    /// <param name="failureReason">Why the run failed or was cancelled, when known.</param>
    Task RecordRunFinishedAsync(Guid jobId, IReadOnlyList<IPipelineStep> steps, ProcessingState terminalState, string? failureReason);
}
