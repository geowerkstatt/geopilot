using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Represents configuration settings that classify a pipeline step's outcome after it has executed.
/// The post-conditions are evaluated in precedence order to mark the step as failed, delivery-restricting,
/// or completed with a warning; if none match, the step succeeds.
/// </summary>
/// <remarks>Use this class to specify expressions that determine a step's terminal state after execution.
/// The conditions are typically evaluated at runtime based on pipeline variables or state.</remarks>
public class PipelineStepPostConditionConfig
{
    /// <summary>
    /// Gets or sets the list of conditions that determine when the step is considered to have failed after execution.
    /// If any condition evaluates to <see langword="true"/>, the step will be marked as failed.
    /// </summary>
    [YamlMember(Alias = "fail_conditions")]
    public List<ConditionConfig>? FailConditions { get; set; }

    /// <summary>
    /// Gets or sets the list of conditions that mark the step with a warning after execution. If no fail
    /// condition matched and any warn condition evaluates to <see langword="true"/>, the step is marked as
    /// a warning and the pipeline continues.
    /// </summary>
    [YamlMember(Alias = "warn_conditions")]
    public List<ConditionConfig>? WarnConditions { get; set; }

    /// <summary>
    /// Gets or sets the list of conditions that restrict delivery after execution. If no fail condition
    /// matched and any restrict-delivery condition evaluates to <see langword="true"/>, the step is marked
    /// as restricting delivery (taking precedence over a warning) and the pipeline continues, but the
    /// produced data may not be delivered.
    /// </summary>
    [YamlMember(Alias = "restrict_delivery_conditions")]
    public List<ConditionConfig>? RestrictDeliveryConditions { get; set; }
}
