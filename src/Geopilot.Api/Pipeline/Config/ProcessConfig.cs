using Geopilot.Api.Pipeline.Process;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration settings for a process, including its name, implementation reference, data handling options, and default parameters.
/// </summary>
public class ProcessConfig
{
    /// <summary>
    /// The unique process identifier. Used to reference this process in <see cref="StepConfig"/>.
    /// </summary>
    [YamlMember(Alias = "id")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Process ID is required.")]
    public required string Id { get; set; }

    /// <summary>
    /// The implementation reference, a fully qualified class identifier for the process logic. Has to have exactly one public method with the <see cref="PipelineProcessRunAttribute"/> attribute which defines the method to be executed when the process is run in a pipeline step.
    /// </summary>
    [YamlMember(Alias = "implementation")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Process Implementation is required.")]
    public required string Implementation { get; set; }

    /// <summary>
    /// Optional default configuration for the process. Can be overridden by <see cref="StepConfig.ProcessConfigOverwrites"/>.
    /// </summary>
    [YamlMember(Alias = "default_config")]
    public Parameterization? DefaultConfig { get; set; }
}
