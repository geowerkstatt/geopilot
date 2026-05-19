using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents a single condition with an expression and an optional localized message.
/// </summary>
public class ConditionConfig
{
    /// <summary>
    /// Gets or sets the boolean expression that is evaluated at runtime.
    /// </summary>
    [YamlMember(Alias = "expression")]
    public required string Expression { get; set; }

    /// <summary>
    /// Gets or sets the localized message associated with this condition.
    /// The dictionary keys are language codes (e.g. "de", "en", "fr", "it") and the values are the messages in the respective language.
    /// </summary>
    [YamlMember(Alias = "message")]
    public Dictionary<string, string>? Message { get; set; }
}
