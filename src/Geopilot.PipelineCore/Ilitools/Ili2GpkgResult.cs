namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Represents the result of an ili2gpkg operation, including success status and log output.
/// </summary>
/// <param name="Success">Indicates whether the operation was successful.</param>
/// <param name="Log">The log output generated during the operation.</param>
public sealed record Ili2GpkgResult(bool Success, string Log);
