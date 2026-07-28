using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Represents configuration options for the Ilitools component of the Geopilot pipeline.
/// </summary>
public class IlitoolsOptions
{
    /// <summary>
    /// The name of the configuration section that contains Ilitools options.
    /// </summary>
    public const string SectionName = "Ilitools";

    /// <summary>
    /// Gets or sets the address of the ilitools-wrapper service, which is used for gRPC communication.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string IlitoolsWrapperAddress { get; set; }
}
