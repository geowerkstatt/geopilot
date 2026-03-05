using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents configuration settings for conditional execution of a pipeline step, including conditions for skipping or
/// failing the step. This condition is evaluated after the step executes, allowing for dynamic control over whether the
/// step should run or be marked as failed based on the current pipeline context and variables.
/// </summary>
/// <remarks>Use this class to specify expressions that determine whether a pipeline step should be skipped or
/// marked as failed. The conditions are typically evaluated at runtime based on pipeline variables or state.</remarks>
public class PipelineStepPostConditionConfig
{
    /// <summary>
    /// Gets or sets the boolean expression that determines when an operation is considered to have failed.
    /// </summary>
    /// <remarks>The condition is typically specified as a string expression evaluated at runtime. If the
    /// condition evaluates to <see langword="true"/>, the step will be failed; otherwise, it will be executed.
    /// The expression typically references the pipeline context data. And evaluates to <see langword="true"/> or
    /// <see langword="false"/> based on the current state of the pipeline.</remarks>
    [YamlMember(Alias = "fail_condition")]
    public required string FailCondition { get; set; }
}
