namespace Geopilot.PipelineCore.Pipeline.Process.Container;

/// <summary>
/// The outcome of running a container to completion.
/// </summary>
/// <param name="ExitCode">The container process exit code. Zero indicates success.</param>
/// <param name="Stdout">Captured standard output.</param>
/// <param name="Stderr">Captured standard error.</param>
/// <param name="Duration">Wall-clock duration of the container run.</param>
public sealed record ContainerRunResult(
    long ExitCode,
    string Stdout,
    string Stderr,
    TimeSpan Duration);
