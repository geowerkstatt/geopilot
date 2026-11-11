using Geopilot.Pipeline;

namespace Geopilot.Api.Models;

/// <summary>
/// The execution protocol record of one processing job: an append-only account of what ran, on which
/// definition version, for whom, and how it ended. Written by the processing services while the job runs
/// and kept independently of the job's retention, so it survives restarts and cleanup. No execution path
/// reads it back to make decisions (see ADR 0010).
/// </summary>
public class PipelineRun
{
    /// <summary>
    /// How many characters the client reference of a machine delivery may have, wherever it is accepted.
    /// </summary>
    public const int ClientReferenceMaxLength = 128;

    /// <summary>
    /// The unique identifier for the run record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The id of the processing job this record describes. Unique: one record per job.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The id of the pipeline that ran, as configured in the definition.
    /// </summary>
    public string PipelineId { get; set; } = string.Empty;

    /// <summary>
    /// The definition snapshot the job executed on, as JSON (see
    /// <see cref="IPipelineFactory.GetDefinitionSnapshotJson"/>): the pipeline as configured, the process
    /// catalog entries its steps reference, and the effective base configuration of those implementations.
    /// Written once and never deserialized back; read via SQL or served as-is.
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// The application version that executed the run.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// The mandate the run was started for. Nullable because the protocol outlives everything else:
    /// a record must remain writable even if no mandate can be attached.
    /// </summary>
    public int? MandateId { get; set; }

    /// <summary>
    /// Navigation to the mandate, when one is attached.
    /// </summary>
    public Mandate? Mandate { get; set; }

    /// <summary>
    /// The user that started the run, or <see langword="null"/> when the job was started anonymously on a
    /// public mandate. Anonymous users may process but not deliver; who declared the delivery is recorded
    /// on the <see cref="Delivery"/> itself, reachable via <see cref="JobId"/>.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Navigation to the user, when one is attached.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// The machine client that started the run, or <see langword="null"/> when a user or an anonymous caller
    /// did. Never set together with <see cref="UserId"/>, which the database enforces.
    /// </summary>
    public int? MachineClientId { get; set; }

    /// <summary>
    /// Navigation to the machine client, when one is attached.
    /// </summary>
    public MachineClient? MachineClient { get; set; }

    /// <summary>
    /// The kind of client that started the run, classified from the request (never the raw header).
    /// Makes unattended machine deliveries countable (re-evaluation trigger of ADR 0010).
    /// </summary>
    public ClientKind ClientKind { get; set; }

    /// <summary>
    /// The client's own identifier of the delivery a machine delivery attempt belongs to, as sent with the attempt,
    /// or <see langword="null"/> when it carried none. Attempts of one delivery share it, which is all the list of
    /// runs needs to group them; it is never checked against earlier attempts.
    /// </summary>
    public string? ClientReference { get; set; }

    /// <summary>
    /// Whether the run can ever become a delivery, decided when its job started (see
    /// <see cref="Processing.ProcessingJob.IsDeclarable"/>). <see langword="null"/> for a run recorded before the
    /// protocol kept it: unknown, which is not the same as not declarable.
    /// </summary>
    public Declarability? Declarability { get; set; }

    /// <summary>
    /// The id of the upload whose files the run processed.
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Where the uploaded files were stored, credential-free (e.g. the blob container URI or the
    /// local root as file URI). Together with each file's storage key this is a resolvable reference;
    /// whether the files still exist there is up to the storage's own retention.
    /// </summary>
    public string UploadStorageLocation { get; set; } = string.Empty;

    /// <summary>
    /// When the upload session was initiated (UTC).
    /// </summary>
    public DateTime UploadInitiatedAt { get; set; }

    /// <summary>
    /// When the job was started (UTC). This is when the record is written.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// The outcome of the malware scan of the uploaded files.
    /// </summary>
    public ScanState ScanState { get; set; }

    /// <summary>
    /// Details of a detected threat, when the scan found one.
    /// </summary>
    public string? ScanDetails { get; set; }

    /// <summary>
    /// The state the job ended in. <see langword="null"/> means the run never reached a terminal state:
    /// the instance died while it ran (restart victim), or the terminal write itself failed. Both read
    /// as "outcome unknown".
    /// </summary>
    public ProcessingState? TerminalState { get; set; }

    /// <summary>
    /// When the job reached its terminal state (UTC).
    /// </summary>
    public DateTime? TerminalAt { get; set; }

    /// <summary>
    /// Why the run failed or was cancelled, when no step says why: the preflight failure reason, a job
    /// timeout, a host shutdown, or an error while persisting the downloads or staging the delivery files.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Why geopilot could not declare the delivery of a machine delivery although the run allowed it, as the caller
    /// of the attempt reads it. <see langword="null"/> when geopilot declared it or had nothing to declare, and unknown
    /// for a run without <see cref="Declarability"/>. Whether a delivery exists is not kept here: the delivery itself
    /// is the record of that, reachable via <see cref="JobId"/>.
    /// </summary>
    public string? DeclarationFailure { get; set; }

    /// <summary>
    /// The manifest of the uploaded files.
    /// </summary>
    public List<PipelineRunFile> Files { get; set; } = new();

    /// <summary>
    /// The per-step records of the run.
    /// </summary>
    public List<PipelineRunStep> Steps { get; set; } = new();

    /// <summary>
    /// What happened to the reactions of the delivery the run produced.
    /// </summary>
    public List<PipelineRunReactionEvent> ReactionEvents { get; set; } = new();
}
