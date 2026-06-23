using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Pipeline.Process.Hop;

/// <summary>
/// Outcome of a single Hop worker job.
/// </summary>
/// <param name="Success">Whether the worker reported success (produced <c>success.log</c>).</param>
/// <param name="OutputFiles">The files collected from the job's <c>output/</c> directory. Empty when the job failed.</param>
/// <param name="Log">Content of the worker-produced log (<c>success.log</c> or <c>error.log</c>).</param>
internal sealed record HopRunResult(bool Success, IReadOnlyList<IPipelineFile> OutputFiles, string Log);
