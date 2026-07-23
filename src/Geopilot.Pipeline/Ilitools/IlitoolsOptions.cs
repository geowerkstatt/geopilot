namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Represents configuration options for the Ilitools component of the Geopilot pipeline.
/// </summary>
public class IlitoolsOptions
{
    /// <summary>
    /// Gets or sets the address of the ilitools-wrapper service, which is used for gRPC communication.
    /// </summary>
    public required string IlitoolsWrapperAddress { get; set; }
}
