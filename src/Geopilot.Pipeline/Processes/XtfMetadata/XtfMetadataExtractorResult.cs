using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfMetadata;

internal class XtfMetadataExtractorResult
{
    public string? Scope { get; init; }

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
    /// An XML root element other than the INTERLIS 2.4 transfer, for example an INTERLIS 2.3 file, so nothing is read
    /// from it.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Not readable as XML, including an object beyond the limits of the extractor.
    /// </summary>
    Unreadable,
}
