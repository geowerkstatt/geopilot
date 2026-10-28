using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class GpkgMetadataExtractorResult
{
    /// <summary>
    /// A semicolon-separated list of model names found in the GeoPackage file.
    /// </summary>
    public string? Models { get; init; }

    public required GpkgFileState FileState { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}

/// <summary>
/// How far the extractor could read a GeoPackage file.
/// </summary>
internal enum GpkgFileState
{
    /// <summary>
    /// A GeoPackage file, read as far as the extraction needed.
    /// </summary>
    Readable,

    /// <summary>
    /// The provided GeoPackage file could be read, but is missing some required data.
    /// </summary>
    MissingData,

    /// <summary>
    /// The provided GeoPackage file is not readable as an SQLite database, or its model table could not be read.
    /// </summary>
    Unreadable,
}
