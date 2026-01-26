using YamlDotNet.Serialization;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for a single step in the <see cref="PipelineConfig"/>.
/// </summary>
internal class StepConfig
{
    /// <summary>
    /// The name of the step, used to identify it within the pipeline. A step is uniquely identified by its name. Used to reference this step in other configurations.
    /// </summary>
    [YamlMember(Alias = "name")]
    public required string Name { get; set; }

    /// <summary>
    /// The process to execute in this step. References the <see cref="ProcessConfig.Name"/> of a defined process.
    /// </summary>
    [YamlMember(Alias = "process")]
    public required string Process { get; set; }

    /// <summary>
    /// Optional configuration overrides for the process in this step. Overrides the default configuration defined in <see cref="ProcessConfig.DefaultConfig"/>.
    /// </summary>
    [YamlMember(Alias = "process_config_overwrites")]
    public Dictionary<string, string>? ProcessConfigOverwrites { get; set; }

    /// <summary>
    /// Configuration for input data handling in this step. Defines how to map data from the input sources to the process.
    /// </summary>
    [YamlMember(Alias = "input")]
    public List<InputConfig> Input { get; set; } = new List<InputConfig>();

    /// <summary>
    /// Configuration for output data handling in this step. Defines how to map data from the process to the output destinations.
    /// </summary>
    [YamlMember(Alias = "output")]
    public List<OutputConfig> Output { get; set; } = new List<OutputConfig>();
}
