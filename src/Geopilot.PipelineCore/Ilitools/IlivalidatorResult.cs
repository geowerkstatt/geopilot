namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Represents the result of an ilivalidator operation. The logs are not part of it: the caller supplies the files
/// they are written to, because both are outputs of the validation and not only diagnostics.
/// </summary>
/// <param name="Success">Indicates whether the validation succeeded.</param>
public sealed record IlivalidatorResult(bool Success);
