using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration settings for a process, including its name, implementation reference, data handling options, and default parameters.
/// </summary>
internal class ProcessConfig
{
    /// <summary>
    /// The process name. Used to reference this process in <see cref="StepConfig"/>.
    /// </summary>
    [YamlMember(Alias = "name")]
    public required string Name { get; set; }

    /// <summary>
    /// The implementation reference, a fully qualified class identifier for the process logic. Has to implement <see cref="Geopilot.Api.Pipeline.Process.IPipelineProcess"/>.
    /// </summary>
    [YamlMember(Alias = "implementation")]
    public required string Implementation { get; set; }

    /// <summary>
    /// Optional data handling configuration for the process. Defines how input and output data are mapped.
    /// </summary>
    [YamlMember(Alias = "data_handling")]
    public DataHandlingConfig? DataHandlingConfig { get; set; }

    /// <summary>
    /// Optional default configuration for the process. Can be overridden by <see cref="StepConfig.ProcessConfigOverwrites"/>.
    /// </summary>
    [YamlMember(Alias = "default_config")]
    public Dictionary<string, string> DefaultConfig { get; set; } = new Dictionary<string, string>();
}
