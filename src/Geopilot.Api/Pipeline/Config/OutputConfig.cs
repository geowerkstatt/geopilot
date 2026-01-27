using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for output data handling in a pipeline step.
/// </summary>
internal class OutputConfig
{
    /// <summary>
    /// The attribute name to take the data from.
    /// </summary>
    [YamlMember(Alias = "take")]
    public required string Take { get; set; }

    /// <summary>
    /// The attribute name to map the output data to.
    /// </summary>
    [YamlMember(Alias = "as")]
    public required string As { get; set; }

    /// <summary>
    /// The action to perform with the output data.
    /// </summary>
    [YamlMember(Alias = "action")]
    public required OutputAction Action { get; set; }
}
