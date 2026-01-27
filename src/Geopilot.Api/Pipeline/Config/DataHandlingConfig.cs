using Geopilot.Api.Pipeline.Process;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for an <see cref="IPipelineProcess"/> for mapping input and output data fields.
/// </summary>
internal class DataHandlingConfig
{
    /// <summary>
    /// Defines how input data fields are mapped. The key is the expected input field name, and the value is the actual data source field name.
    /// </summary>
    [YamlMember(Alias = "input_mapping")]
    public Dictionary<string, string> InputMapping { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Defines how output data fields are mapped. The key is the expected output field name, and the value is the actual data source field name.
    /// </summary>
    [YamlMember(Alias = "output_mapping")]
    public Dictionary<string, string> OutputMapping { get; set; } = new Dictionary<string, string>();
}
