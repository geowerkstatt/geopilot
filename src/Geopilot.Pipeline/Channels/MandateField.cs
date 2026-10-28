using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Channels;

/// <summary>
/// What a mandate supplies for a value a channel takes from it.
/// </summary>
public enum MandateValueType
{
    /// <summary>A single text.</summary>
    Text,

    /// <summary>A list of texts.</summary>
    TextList,

    /// <summary>A switch, on or off.</summary>
    Switch,
}

/// <summary>
/// A value a reaction set asks the mandate for, as the administration shows it in the form of the mandate.
/// </summary>
/// <param name="ReactionId">The reaction the value is for.</param>
/// <param name="Key">The key of the value, the name of the parameter of the channel.</param>
/// <param name="DisplayName">The label of the field, as <c>mandate_config</c> declares it.</param>
/// <param name="Type">Whether the value is a text, a list of texts or a switch.</param>
/// <param name="Required">Whether the mandate must supply the value, because the channel cannot do without it.</param>
public record MandateField(string ReactionId, string Key, LocalizedText DisplayName, MandateValueType Type, bool Required);
