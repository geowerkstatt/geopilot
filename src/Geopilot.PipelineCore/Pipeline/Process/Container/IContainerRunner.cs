namespace Geopilot.PipelineCore.Pipeline.Process.Container;

/// <summary>
/// Abstraction for running a one-shot container to perform pipeline work.
/// Implementations are responsible for pulling the image, running the container,
/// capturing output, and cleaning up resources.
/// </summary>
public interface IContainerRunner
{
    /// <summary>
    /// Runs the container described by <paramref name="spec"/> and returns the result after it exits.
    /// </summary>
    /// <param name="spec">The container run specification.</param>
    /// <param name="cancellationToken">Cancellation token. On cancellation, the container is killed and removed.</param>
    /// <returns>The exit code and captured stdout/stderr.</returns>
    /// <exception cref="ContainerRunException">Thrown if the image is not allowed, cannot be pulled, or the container fails to start.</exception>
    Task<ContainerRunResult> RunAsync(ContainerRunSpec spec, CancellationToken cancellationToken);
}
