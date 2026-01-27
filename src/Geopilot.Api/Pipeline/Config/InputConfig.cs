using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for input data handling in a pipeline step.
/// </summary>
internal class InputConfig
{
    /// <summary>
    /// The source to take input data from.
    /// </summary>
    [YamlMember(Alias = "from")]
    public string? From { get; set; }

    /// <summary>
    /// The attribute name to take the input data from.
    /// </summary>
    [YamlMember(Alias = "take")]
    public required string Take { get; set; }

    /// <summary>
    /// The attribute name to map the input data to.
    /// </summary>
    [YamlMember(Alias = "as")]
    public required string As { get; set; }
}
