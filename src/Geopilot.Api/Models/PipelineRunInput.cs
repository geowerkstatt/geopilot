using Geopilot.Pipeline;

namespace Geopilot.Api.Models;

/// <summary>
/// One value a step's process was given when it started, with where it came from: an element the input binding bound
/// to the run method, or a file the process configuration supplied to the constructor. Kept in the protocol so "what did
/// this step work on" stays answerable after the files themselves are gone.
/// </summary>
public class PipelineRunInput
{
    /// <summary>
    /// The unique identifier for the input record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The step record this input belongs to.
    /// </summary>
    public int PipelineRunStepId { get; set; }

    /// <summary>
    /// The name of the run method parameter, or of the constructor parameter for a configured file.
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// Where the value comes from.
    /// </summary>
    public InputOrigin Origin { get; set; }

    /// <summary>
    /// The referenced output as <c>stepId.Property</c>, or for a resource or a configured file its path below the
    /// resources directory.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Whether the input is a value, a file or a reference to a step that was skipped.
    /// </summary>
    public InputKind Kind { get; set; }

    /// <summary>
    /// For a value its text, formatted with the invariant culture and cut after 500 characters.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// For a file its original name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// For a file its size in bytes; for an uploaded file the declared size of the manifest.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// The SHA-256 of the file content as lowercase hex: for an upload the one of the manifest, when the file was
    /// scanned; for a resource or a configured file computed when the step started.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// For a file whether it is an uploaded file passed on unchanged.
    /// </summary>
    public bool? FromUpload { get; set; }
}
