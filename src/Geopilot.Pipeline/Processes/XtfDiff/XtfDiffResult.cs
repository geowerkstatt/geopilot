using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfDiff;

internal class XtfDiffResult
{
    public required ComparisonState ComparisonState { get; init; }

    public required LocalizedText StatusMessage { get; init; }

    /// <summary>
    /// The changes from the old to the new state as a JSON array, as the XTF-Diff-Tool writes them. Absent when the two
    /// states could not be compared.
    /// </summary>
    public IPipelineFile? Diff { get; init; }

    public required IPipelineFile Log { get; init; }
}

/// <summary>
/// Whether the XTF-Diff-Tool could compare the two states.
/// </summary>
internal enum ComparisonState
{
    /// <summary>
    /// The two states were compared; the diff lists their changes, possibly none.
    /// </summary>
    Compared,

    /// <summary>
    /// The two states use different models or INTERLIS versions, so there is no diff. Whether that blocks the delivery
    /// is up to the pipeline: with two uploads it is a wrong file, against a previous delivery a change of the model.
    /// </summary>
    NotComparable,
}
