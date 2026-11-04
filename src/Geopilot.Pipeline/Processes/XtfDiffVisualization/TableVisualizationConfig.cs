namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// The table view of the XTF diff visualization. The map beside it reads the same changes for its popup and its
/// filter.
/// </summary>
internal sealed record TableVisualizationConfig
{
    /// <summary>
    /// Gets every change of the diff, in the order of the diff.
    /// </summary>
    public required IReadOnlyList<DiffChange> Changes { get; init; }
}
