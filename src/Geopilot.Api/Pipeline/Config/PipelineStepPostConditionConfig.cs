using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents configuration settings for conditional execution of a pipeline step after it has executed.
/// If any condition evaluates to <see langword="true"/>, the step will be marked as failed.
/// </summary>
/// <remarks>Use this class to specify expressions that determine whether a pipeline step should be
/// marked as failed after execution. The conditions are typically evaluated at runtime based on pipeline variables or state.</remarks>
public class PipelineStepPostConditionConfig
{
    /// <summary>
    /// Gets or sets the list of conditions that determine when the step is considered to have failed after execution.
    /// If any condition evaluates to <see langword="true"/>, the step will be marked as failed.
    /// </summary>
    [YamlMember(Alias = "fail_conditions")]
    public List<ConditionConfig>? FailConditions { get; set; }
}
