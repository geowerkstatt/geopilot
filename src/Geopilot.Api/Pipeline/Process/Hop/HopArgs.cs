namespace Geopilot.Api.Pipeline.Process.Hop;

/// <summary>
/// Per-run arguments serialized to <c>args.json</c> for the Hop worker.
/// </summary>
/// <param name="Pipeline">Name of the Hop pipeline or workflow file (<c>.hpl</c>/<c>.hwf</c>) relative to the worker's pipeline-definition directory.</param>
/// <param name="Parameters">Key-value pairs forwarded as <c>-p</c> parameters to <c>hop-run</c>. May be empty.</param>
internal sealed record HopArgs(string Pipeline, IReadOnlyDictionary<string, string> Parameters);
