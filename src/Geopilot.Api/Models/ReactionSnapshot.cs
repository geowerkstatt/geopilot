using Geopilot.Pipeline.Config;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Models;

/// <summary>
/// What a reaction keeps from its set when the delivery is declared, stored as <see cref="DeliveryReaction.Config"/>,
/// so a later change to the set or the mandate does not reach a delivery that is already declared. The values of the
/// pipeline definition and of the mandate stay apart, so a mandate value still only reaches a parameter the channel
/// marks for it. The base configuration is not part of it: every attempt reads it anew, so the credentials it holds
/// stay out of the snapshot.
/// </summary>
/// <param name="Config">The configuration the pipeline definition gives the channel.</param>
/// <param name="Mandate">The values the mandate supplies to the reaction.</param>
public sealed record ReactionSnapshot(
    [property: JsonPropertyName("config")] Parameterization Config,
    [property: JsonPropertyName("mandate")] Dictionary<string, JsonElement> Mandate)
{
    /// <summary>
    /// Reads a snapshot back from the JSON it is stored as.
    /// </summary>
    internal static ReactionSnapshot FromJson(string json)
        => JsonSerializer.Deserialize<ReactionSnapshot>(json) ?? throw new InvalidOperationException("A stored reaction snapshot is never null.");
}
