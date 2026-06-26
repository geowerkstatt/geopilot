namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// A parsed validation error paired with the stable id used to correlate its map feature and tree node.
/// </summary>
/// <param name="Id">The stable error id (its position in the parsed log).</param>
/// <param name="Error">The parsed error.</param>
internal sealed record IndexedError(string Id, LogError Error);
