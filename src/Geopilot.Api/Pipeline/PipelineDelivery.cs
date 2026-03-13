namespace Geopilot.Api.Pipeline;

/// <summary>
/// Specifies whether pipeline data delivery is allowed or prevented.
/// </summary>
/// <remarks>Use this enumeration to control the behavior of pipeline data delivery in relevant operations. The value
/// determines if the pipeline can proceed or if delivery should be blocked.</remarks>
public enum PipelineDelivery
{
    /// <summary>
    /// Gets or sets a value indicating whether the pipeline data delivery is permitted.
    /// </summary>
    Allow,

    /// <summary>
    /// Gets or sets a value indicating whether the pipeline data delivery is prevented.
    /// </summary>
    Prevent,
}
