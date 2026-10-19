namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Represents the result of an ili2gpkg operation.
/// </summary>
/// <param name="Success">Indicates whether the operation was successful.</param>
public sealed record Ili2GpkgResult(bool Success);
