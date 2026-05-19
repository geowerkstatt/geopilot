using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents configuration settings for conditional execution of a pipeline step, including conditions for skipping or
/// failing the step. This condition is evaluated before the step executes, allowing for dynamic control over whether the
/// step should run or be marked as failed or skipped based on the current pipeline context and variables.
/// </summary>
/// <remarks>Use this class to specify expressions that determine whether a pipeline step should be skipped or
/// marked as failed. The conditions are typically evaluated at runtime based on pipeline variables or state.</remarks>
public class PipelineStepPreConditionConfig
{
    /// <summary>
    /// Gets or sets the list of conditions that determine whether the associated step should be skipped.
    /// If any condition evaluates to <see langword="true"/>, the step will be skipped.
    /// </summary>
    [YamlMember(Alias = "skip_conditions")]
    public List<ConditionConfig>? SkipConditions { get; set; }

    /// <summary>
    /// Gets or sets the list of conditions that determine when the step is considered to have failed.
    /// If any condition evaluates to <see langword="true"/>, the step will be marked as failed and not executed.
    /// </summary>
    [YamlMember(Alias = "fail_conditions")]
    public List<ConditionConfig>? FailConditions { get; set; }
}
