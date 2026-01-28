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
    /// The step identifier. It uniquely identifies the step within the given pipeline. It is used to reference this step from other configurations.
    /// </summary>
    [YamlMember(Alias = "id")]
    [Required(AllowEmptyStrings = false)]
    public required string Id { get; set; }

    /// <summary>
    /// The steps display name. A human-readable name for the step.
    /// </summary>
    [YamlMember(Alias = "display_name")]
    [Required(AllowEmptyStrings = false)]
    public required Dictionary<string, string> DisplayName { get; set; }

    /// <summary>
    /// The process to execute in this step. References the <see cref="ProcessConfig.Id"/> of a defined process.
    /// </summary>
    [YamlMember(Alias = "process_id")]
    [Required(AllowEmptyStrings = false)]
    public string? ProcessId { get; set; }

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
    public List<InputConfig>? Input { get; set; }

    /// <summary>
    /// Configuration for output data handling in this step. Defines how to map data from the process to the output destinations.
    /// </summary>
    [YamlMember(Alias = "output")]
    [Required]
    public List<OutputConfig>? Output { get; set; }
}
