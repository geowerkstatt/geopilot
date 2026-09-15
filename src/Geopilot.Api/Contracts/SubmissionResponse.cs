using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The state of a machine delivery attempt.
/// </summary>
/// <param name="Id">The id of the attempt, which is also the id of its processing job.</param>
/// <param name="State">Where the attempt stands.</param>
/// <param name="MandateKey">The mandate the attempt was started for.</param>
/// <param name="DeliveryId">The delivery that was created, once it exists.</param>
/// <param name="Messages">What the run reported, per step.</param>
/// <param name="Downloads">The files the run offers for download, whatever its steps tagged as such.</param>
public record SubmissionResponse(
    Guid Id,
    SubmissionState State,
    string MandateKey,
    int? DeliveryId,
    IReadOnlyList<SubmissionMessage> Messages,
    IReadOnlyList<SubmissionDownload> Downloads);

/// <summary>
/// Something the run reported about one of its steps.
/// </summary>
/// <param name="Step">The step that reported it.</param>
/// <param name="Severity">How much it weighs.</param>
/// <param name="Text">The message, per language.</param>
public record SubmissionMessage(string Step, SubmissionMessageSeverity Severity, LocalizedText Text);

/// <summary>
/// A file the run offers for download, available while the attempt lives. What a step offers is its own
/// decision: geopilot knows only that it was tagged for download, not whether it is a protocol, a report or
/// converted data.
/// </summary>
/// <param name="Step">The step that produced it.</param>
/// <param name="Name">The name the step gave the file. Together with <c>step</c> it identifies the file, and it is the name the download is served under. It is not part of the URL, because two steps may produce the same name.</param>
/// <param name="Url">Where to download it, and the only supported way to fetch it. Do not build a URL from <c>name</c>.</param>
public record SubmissionDownload(string Step, string Name, Uri Url);

/// <summary>
/// Where a machine delivery attempt stands.
/// </summary>
public enum SubmissionState
{
    /// <summary>The run is still going, or its delivery has not been written yet.</summary>
    Processing,

    /// <summary>The pipeline accepted the data and the delivery was created.</summary>
    Delivered,

    /// <summary>The pipeline checked the data and refused it, so no delivery was created.</summary>
    Rejected,

    /// <summary>The attempt did not get far enough for the pipeline to judge the data, or the delivery could not be written.</summary>
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
