using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfMetadata;

internal class XtfMetadataExtractorResult
{
    public string? Scope { get; init; }

    /// <summary>
    /// The INTERLIS version of the transfer file, named like <see cref="Processes.InterlisVersion"/>, or null when it
    /// is not recognized. Read from every file, whatever its <see cref="FileState"/>.
    /// </summary>
    /// <remarks>A single value, which a condition compares with <c>==</c>. Text like the versions of the XTF matcher, so
    /// the same value has one type: their list needs text, because <c>in</c> never matches an enum value.</remarks>
    public string? InterlisVersion { get; init; }

    public required XtfFileState FileState { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}

/// <summary>
/// How far the extractor could read a transfer file.
/// </summary>
internal enum XtfFileState
{
    /// <summary>
    /// An INTERLIS 2.4 transfer, read as far as the extraction needed.
    /// </summary>
    Readable,

    /// <summary>
    /// An XML root element other than the INTERLIS 2.4 transfer, for example an INTERLIS 2.3 file, so nothing beyond
    /// its INTERLIS version is read from it.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Not readable as XML, including an object beyond the limits of the extractor.
    /// </summary>
    Unreadable,
}
