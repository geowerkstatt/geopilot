using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for conditional execution of a pipeline step, including pre- and post-step conditions.
/// </summary>
/// <remarks>Use this class to specify conditions that must be met before or after a pipeline step executes. The
/// configuration allows for fine-grained control over step execution flow within a pipeline.</remarks>
public class PipelineStepConditionsConfig
{
    /// <summary>
    /// Gets or sets the condition that are evaluated before the pipeline step executes.
    /// </summary>
    [YamlMember(Alias = "pre")]
    public PipelineStepPreConditionConfig? Pre { get; set; }

    /// <summary>
    /// Gets or sets the condition that are evaluated after the pipeline step executes.
    /// </summary>
    [YamlMember(Alias = "post")]
    public PipelineStepPostConditionConfig? Post { get; set; }
}
