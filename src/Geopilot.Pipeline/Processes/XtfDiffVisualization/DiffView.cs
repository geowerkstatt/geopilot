namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// A view the XTF diff visualization can include, configured with <c>include</c>.
/// </summary>
internal enum DiffView
{
    /// <summary>
    /// The map of the changed objects, with a popup naming the changes of an object.
    /// </summary>
    Map,

    /// <summary>
    /// The table of the changes, with the changes of the selected object below it.
    /// </summary>
    Table,
}
