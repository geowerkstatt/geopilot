using Geopilot.Api.Models;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The execution protocol of one processing job: what ran, on which definition version, for whom, and how
/// it ended. A <see cref="TerminalState"/> of <see langword="null"/> means the outcome is unknown, the
/// instance died while the job ran. The definition snapshot itself is served by its own endpoint.
/// </summary>
/// <param name="JobId">The id of the processing job.</param>
/// <param name="PipelineId">The id of the pipeline that ran.</param>
/// <param name="AppVersion">The application version that executed the run.</param>
/// <param name="MandateId">The mandate the run was started for.</param>
/// <param name="UserId">The user that started the run, or <see langword="null"/> when the job was started anonymously. Who declared the delivery is recorded on the delivery itself.</param>
/// <param name="ClientKind">The kind of client that started the run.</param>
/// <param name="UploadId">The id of the upload the run processed.</param>
/// <param name="UploadStorageLocation">Where the uploaded files were stored, credential-free.</param>
/// <param name="UploadInitiatedAt">When the upload session was initiated (UTC).</param>
/// <param name="StartedAt">When the job was started (UTC).</param>
/// <param name="ScanState">The outcome of the malware scan.</param>
/// <param name="ScanDetails">Details of a detected threat, when the scan found one.</param>
/// <param name="TerminalState">The state the job ended in, or <see langword="null"/> when the outcome is unknown.</param>
/// <param name="TerminalAt">When the job reached its terminal state (UTC).</param>
/// <param name="FailureReason">Why the run failed or was cancelled, when known.</param>
/// <param name="Files">The manifest of the uploaded files.</param>
/// <param name="Steps">The per-step records, in pipeline order.</param>
public record PipelineRunResponse(
    Guid JobId,
    string PipelineId,
    string AppVersion,
    int? MandateId,
    int? UserId,
    ClientKind ClientKind,
    Guid UploadId,
    string UploadStorageLocation,
    DateTime UploadInitiatedAt,
    DateTime StartedAt,
    ScanState ScanState,
    string? ScanDetails,
    ProcessingState? TerminalState,
    DateTime? TerminalAt,
    string? FailureReason,
    List<PipelineRunFileResponse> Files,
    List<PipelineRunStepResponse> Steps);

/// <summary>
/// One uploaded file of the run.
/// </summary>
/// <param name="FileName">The original file name as uploaded.</param>
/// <param name="StorageKey">The storage key relative to the run's storage location.</param>
/// <param name="DeclaredSize">The file size in bytes as declared and verified by the preflight.</param>
/// <param name="Sha256">The SHA-256 of the content as lowercase hex, when the file was scanned.</param>
public record PipelineRunFileResponse(
    string FileName,
    string StorageKey,
    long DeclaredSize,
    string? Sha256);

/// <summary>
/// The protocol record of one pipeline step.
/// </summary>
/// <param name="StepId">The step id from the pipeline definition.</param>
/// <param name="DisplayName">The step's localized display name.</param>
/// <param name="ProcessImplementation">The fully qualified type name of the process implementation.</param>
/// <param name="ProcessAssemblyName">The assembly the implementation came from.</param>
/// <param name="ProcessAssemblyVersion">The version of that assembly.</param>
/// <param name="State">The recorded state of the step.</param>
/// <param name="StartedAt">When the pipeline reached the step (UTC).</param>
/// <param name="FinishedAt">When the step finished (UTC).</param>
/// <param name="ErrorMessage">The message of the exception that failed or cancelled the step.</param>
/// <param name="StatusMessage">The localized status message the process emitted.</param>
/// <param name="ConditionMessage">The localized message of the conditions that determined the state.</param>
/// <param name="Conditions">The evaluation result of every checked condition, matching or not.</param>
/// <param name="Artifacts">The artifacts the step produced, by name.</param>
public record PipelineRunStepResponse(
    string StepId,
    LocalizedText DisplayName,
    string ProcessImplementation,
    string? ProcessAssemblyName,
    string? ProcessAssemblyVersion,
    StepState State,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage,
    LocalizedText? StatusMessage,
    LocalizedText? ConditionMessage,
    List<PipelineRunConditionResponse> Conditions,
    List<PipelineRunArtifactResponse> Artifacts);

/// <summary>
/// The evaluation result of one step condition.
/// </summary>
/// <param name="ConditionId">The stable identifier from the pipeline definition, when configured.</param>
/// <param name="Phase">The phase the condition was evaluated in.</param>
/// <param name="Kind">The effect a match has on the step.</param>
/// <param name="Expression">The evaluated boolean expression.</param>
/// <param name="Matched">Whether the expression evaluated to true.</param>
/// <param name="EvaluatedValues">The values the expression referenced, rendered per parameter name.</param>
public record PipelineRunConditionResponse(
    string? ConditionId,
    ConditionPhase Phase,
    ConditionKind Kind,
    string Expression,
    bool Matched,
    Dictionary<string, string?>? EvaluatedValues);

/// <summary>
/// One artifact a step produced.
/// </summary>
/// <param name="Kind">What the artifact is.</param>
/// <param name="OriginalFileName">The human-readable original file name.</param>
/// <param name="PersistedFileName">The file name it was persisted under.</param>
/// <param name="FromUpload">For a delivery file: whether it entered the pipeline as an upload.</param>
public record PipelineRunArtifactResponse(
    ArtifactKind Kind,
    string OriginalFileName,
    string PersistedFileName,
    bool? FromUpload);
