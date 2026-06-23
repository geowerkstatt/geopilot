namespace Geopilot.Api.Test.Pipeline.Process;

/// <summary>
/// What the fake Hop worker observed the client write into a job folder before it produced output.
/// </summary>
/// <param name="ArgsJson">Raw content of <c>args.json</c>.</param>
/// <param name="InputFiles">Files seen under <c>input/</c>, keyed by forward-slash relative path, valued by content.</param>
internal sealed record HopJobObservation(string ArgsJson, IReadOnlyDictionary<string, string> InputFiles);
