using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The state of a machine delivery attempt.
/// </summary>
/// <param name="Id">The id of the attempt, which is also the id of its processing job.</param>
/// <param name="State">Where the attempt stands.</param>
/// <param name="Validation">Whether the delivered data passed the checks the pipeline applies.</param>
/// <param name="MandateKey">The mandate the attempt was started for.</param>
/// <param name="DeliveryId">The delivery that was created, once it exists.</param>
/// <param name="Messages">What the run reported, per step.</param>
/// <param name="Logs">The files the run produced for inspection, for example the validation log.</param>
public record SubmissionResponse(
    Guid Id,
    SubmissionState State,
    SubmissionValidation Validation,
    string MandateKey,
    int? DeliveryId,
    IReadOnlyList<SubmissionMessage> Messages,
    IReadOnlyList<SubmissionLog> Logs);

/// <summary>
/// Something the run reported about one of its steps.
/// </summary>
/// <param name="Step">The step that reported it.</param>
/// <param name="Severity">How much it weighs.</param>
/// <param name="Text">The message, per language.</param>
public record SubmissionMessage(string Step, SubmissionMessageSeverity Severity, LocalizedText Text);

/// <summary>
/// A file the run produced, downloadable while the attempt lives.
/// </summary>
/// <param name="Step">The step that produced it.</param>
/// <param name="Name">The name to request it under.</param>
/// <param name="Url">Where to download it.</param>
public record SubmissionLog(string Step, string Name, Uri Url);

/// <summary>
/// Where a machine delivery attempt stands.
/// </summary>
public enum SubmissionState
{
    /// <summary>The run is still going, or its delivery has not been declared yet.</summary>
    Processing,

    /// <summary>The data passed and the delivery was created.</summary>
    Delivered,

    /// <summary>The data was checked and refused, so no delivery was created.</summary>
    Rejected,

    /// <summary>The attempt did not get far enough to judge the data, or the delivery could not be created.</summary>
    Failed,
}

/// <summary>
/// Whether the delivered data passed the checks the pipeline applies.
/// </summary>
public enum SubmissionValidation
{
    /// <summary>The checks did not run, or did not run to completion.</summary>
    NotRun,

    /// <summary>The data passed.</summary>
    Successful,

    /// <summary>The data was checked and did not pass.</summary>
    Failed,
}

/// <summary>
/// How much a message weighs.
/// </summary>
public enum SubmissionMessageSeverity
{
    /// <summary>Reported in passing.</summary>
    Info,

    /// <summary>Reported as a problem that did not stop the delivery.</summary>
    Warning,

    /// <summary>Reported as a problem that stopped the delivery.</summary>
    Error,
}
