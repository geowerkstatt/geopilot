using System.Text.Json;

namespace Geopilot.Api.Services;

/// <summary>
/// Checks the reaction set a mandate chooses and the values the mandate supplies to its reactions.
/// </summary>
public interface IReactionSetValidator
{
    /// <summary>
    /// Checks a set against the pipeline definition and the delete window, and the values of a mandate against the
    /// channels of the set. Values for reactions or keys the set does not declare are dropped.
    /// </summary>
    /// <param name="reactionSetId">The id of the chosen set, or <see langword="null"/> for none.</param>
    /// <param name="values">The values per reaction id and key.</param>
    ReactionSetCheck Check(string? reactionSetId, IReadOnlyDictionary<string, Dictionary<string, JsonElement>> values);
}

/// <summary>
/// The outcome of <see cref="IReactionSetValidator.Check"/>.
/// </summary>
/// <param name="Values">The values, reduced to the keys the set declares.</param>
/// <param name="SetError">Why the set cannot be used at all, or <see langword="null"/>.</param>
/// <param name="ValueErrors">What the channels of the set refuse in the values.</param>
public sealed record ReactionSetCheck(
    Dictionary<string, Dictionary<string, JsonElement>> Values,
    string? SetError,
    IReadOnlyList<string> ValueErrors)
{
    /// <summary>
    /// Whether the set and its values can be saved.
    /// </summary>
    public bool IsValid => SetError is null && ValueErrors.Count == 0;
}
