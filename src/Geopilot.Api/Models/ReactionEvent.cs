namespace Geopilot.Api.Models;

/// <summary>
/// What happened to a reaction, as the execution protocol records it. Persisted as text.
/// </summary>
public enum ReactionEvent
{
    /// <summary>An attempt of the reaction ran through its channel.</summary>
    Attempt,

    /// <summary>The delivery of the reaction was deleted; the state says how far the reaction had got.</summary>
    Deletion,
}
