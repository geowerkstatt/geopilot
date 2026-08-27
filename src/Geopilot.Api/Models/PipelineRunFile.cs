namespace Geopilot.Api.Models;

/// <summary>
/// One uploaded file of a <see cref="PipelineRun"/>: the upload manifest entry the protocol keeps after
/// the blobs themselves are gone.
/// </summary>
public class PipelineRunFile
{
    /// <summary>
    /// The unique identifier for the file record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The run this file belongs to.
    /// </summary>
    public int PipelineRunId { get; set; }

    /// <summary>
    /// Navigation to the run.
    /// </summary>
    public PipelineRun PipelineRun { get; set; } = null!;

    /// <summary>
    /// The original file name as uploaded.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The storage key the file was stored under, relative to the run's
    /// <see cref="PipelineRun.UploadStorageLocation"/>.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// The file size in bytes as declared by the client. The preflight rejects any deviation, so on a
    /// started run this equals the actual size.
    /// </summary>
    public long DeclaredSize { get; set; }

    /// <summary>
    /// The SHA-256 of the file content as hex string, computed server-side while the file was scanned.
    /// <see langword="null"/> when scanning is disabled or the file was never scanned.
    /// </summary>
    public string? Sha256 { get; set; }
}
