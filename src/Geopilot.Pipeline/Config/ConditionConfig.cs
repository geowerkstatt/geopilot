using Geopilot.PipelineCore.Pipeline;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Represents a single condition with an expression and an optional localized message.
/// </summary>
public class ConditionConfig
{
    /// <summary>
    /// Gets or sets the optional stable identifier of this condition. It is recorded with the condition's
    /// evaluation result, so a consumer can reference the reason for a step outcome independently of the
    /// wording of <see cref="Message"/> or the exact <see cref="Expression"/>. Uniqueness must be given
    /// for all conditions within a step <see cref="StepConfig"/>.
    /// </summary>
    [YamlMember(Alias = "id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the boolean expression that is evaluated at runtime.
    /// </summary>
    [YamlMember(Alias = "expression")]
    public required string Expression { get; set; }

    /// <summary>
    /// Gets or sets the localized message associated with this condition.
    /// </summary>
    [YamlMember(Alias = "message")]
    public LocalizedText? Message { get; set; }
}
