using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfDiff;

internal class XtfDiffResult
{
    public required ComparisonState ComparisonState { get; init; }

    public required LocalizedText StatusMessage { get; init; }

    /// <summary>
    /// The changes from the old to the new state as a JSON array, as the XTF-Diff-Tool writes them: one file, or none when
    /// the two states could not be compared. A list, because a download of an empty list offers nothing, while a download
    /// of a missing file fails the run.
    /// </summary>
    public required IPipelineFile[] Diff { get; init; }

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
    /// The tool could not compare the two states, so there is no diff: they use different models or INTERLIS versions, or
    /// a file is broken, for example by a duplicate TID. The status message and the log name the cause. Whether that blocks
    /// the delivery is up to the pipeline: with two uploads it is a wrong file, against a previous delivery mostly a change
    /// of the model.
    /// </summary>
    NotComparable,
}
