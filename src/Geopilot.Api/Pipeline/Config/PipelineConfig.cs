using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for a pipeline.
/// </summary>
internal class PipelineConfig
{
    /// <summary>
    /// The name of the pipeline. A pipeline is uniquely identified by its name.
    /// </summary>
    [YamlMember(Alias = "name")]
    public required string Name { get; set; }

    /// <summary>
    /// The parameters for the pipeline.
    /// </summary>
    [YamlMember(Alias = "parameters")]
    public PipelineParametersConfig? Parameters { get; set; }

    /// <summary>
    /// The steps in the pipeline that will be executed sequentially. Each step defines a process to execute and its data handling configuration.
    /// </summary>
    [YamlMember(Alias = "steps")]
    public List<StepConfig> Steps { get; set; } = new List<StepConfig>();
}
