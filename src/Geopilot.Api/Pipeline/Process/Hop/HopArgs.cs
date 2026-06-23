namespace Geopilot.Api.Pipeline.Process.Hop;

/// <summary>
/// Per-run arguments serialized to <c>args.json</c> for the Hop worker.
/// </summary>
/// <param name="Pipeline">Absolute path of the Hop pipeline or workflow file (<c>.hpl</c>/<c>.hwf</c>) within the worker, passed straight to <c>hop-run --file</c>.</param>
/// <param name="Parameters">Key-value pairs forwarded as <c>-p</c> parameters to <c>hop-run</c>. May be empty.</param>
internal sealed record HopArgs(string Pipeline, IReadOnlyDictionary<string, string> Parameters);
