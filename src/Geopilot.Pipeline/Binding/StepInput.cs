namespace Geopilot.Pipeline;

/// <summary>
/// Where a value a step's process was given comes from: one of the four input sources of a step definition, or the
/// process configuration for a file the process receives through its constructor.
/// </summary>
public enum InputOrigin
{
    /// <summary>A value written in the step definition.</summary>
    Definition,

    /// <summary>An output of an earlier step, <c>${step_output(stepId.Property)}</c>.</summary>
    StepOutput,

    /// <summary>A file below the resources directory, <c>${file(path)}</c>.</summary>
    Resource,

    /// <summary>A file of the upload, <c>${upload()}</c>.</summary>
    Upload,

    /// <summary>A file below the resources directory that the process configuration names for a constructor parameter.</summary>
    Configuration,
}

/// <summary>
/// What a recorded step input is.
/// </summary>
public enum InputKind
{
    /// <summary>A value, recorded as text.</summary>
    Value,

    /// <summary>A file, recorded by name and size.</summary>
    File,

    /// <summary>
    /// A reference to an output of a step that was skipped. It contributes nothing to its parameter, the way the input
    /// binding treats it.
    /// </summary>
    Absent,
}

/// <summary>
/// One value a step's process was given when it started: an element the input binding bound to a parameter of the run
/// method, or a file the process configuration supplied to the constructor. A list contributes one entry per element,
/// each with the origin of the source it came from. A source that yields no element contributes no entry, except a
/// reference to a skipped step, which is recorded as <see cref="InputKind.Absent"/>.
/// </summary>
/// <param name="Parameter">The name of the run method parameter, or of the constructor parameter for a configured file.</param>
/// <param name="Origin">Where the value comes from.</param>
/// <param name="Reference">The referenced output as <c>stepId.Property</c> for <see cref="InputOrigin.StepOutput"/>, the path below the resources directory for <see cref="InputOrigin.Resource"/> and <see cref="InputOrigin.Configuration"/>, otherwise <see langword="null"/>.</param>
/// <param name="Kind">Whether the entry is a value, a file or an absent reference.</param>
/// <param name="Value">For a value its text, formatted with the invariant culture, cut after 500 characters, a nested list reduced to its count. <see langword="null"/> for a null value and for any other kind.</param>
/// <param name="FileName">For a file its original name.</param>
/// <param name="Size">For a file its size in bytes, when the runtime knows it without fetching the file. The runtime leaves it open for an uploaded file, whose declared size the host knows.</param>
/// <param name="Sha256">For a resource or a configured file the SHA-256 of its content as lowercase hex. The runtime computes no hash for other files.</param>
/// <param name="FromUpload">For a file whether it is an uploaded file passed on unchanged, also when an earlier step passed it on. <see langword="null"/> for any other kind.</param>
public sealed record StepInput(
    string Parameter,
    InputOrigin Origin,
    string? Reference,
    InputKind Kind,
    string? Value = null,
    string? FileName = null,
    long? Size = null,
    string? Sha256 = null,
    bool? FromUpload = null);
