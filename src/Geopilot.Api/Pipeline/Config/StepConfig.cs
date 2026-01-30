using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for a single step in the <see cref="PipelineConfig"/>.
/// </summary>
internal class StepConfig
{
    /// <summary>
    /// The name of the step. It uniquely identifies the step within the given pipeline. It is used to reference this step from other configurations.
    /// </summary>
    [YamlMember(Alias = "name")]
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    /// <summary>
    /// The process to execute in this step. References the <see cref="ProcessConfig.Name"/> of a defined process.
    /// </summary>
    [YamlMember(Alias = "process")]
    [Required(AllowEmptyStrings = false)]
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
    [Required]
    public required List<InputConfig> Input { get; set; }

    /// <summary>
    /// Configuration for output data handling in this step. Defines how to map data from the process to the output destinations.
    /// </summary>
    [YamlMember(Alias = "output")]
    [Required]
    public required List<OutputConfig> Output { get; set; }
}
