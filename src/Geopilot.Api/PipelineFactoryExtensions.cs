using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api;

/// <summary>
/// Reads what the administration shows from the pipeline definition as it is now.
/// </summary>
internal static class PipelineFactoryExtensions
{
    /// <summary>
    /// The names of the reactions in the definition as it is now, by id. Reaction ids are unique across all sets, and a
    /// reaction the definition no longer has is missing, so it keeps its id only.
    /// </summary>
    internal static IReadOnlyDictionary<string, LocalizedText> ReactionDisplayNames(this IPipelineFactory pipelineFactory)
        => pipelineFactory.ReactionSets
            .SelectMany(set => set.AllReactions())
            .ToDictionary(reaction => reaction.Id, reaction => reaction.DisplayName, StringComparer.Ordinal);
}
