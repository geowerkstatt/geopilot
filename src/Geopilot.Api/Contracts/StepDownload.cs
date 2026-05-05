namespace Geopilot.Api.Contracts;

/// <summary>
/// A single downloadable file produced by a processing step.
/// </summary>
/// <param name="OriginalFileName">The human-readable file name (suitable for the browser's "save as" dialog).</param>
/// <param name="Url">Absolute URL to download the file.</param>
public record StepDownload(string OriginalFileName, Uri Url);
