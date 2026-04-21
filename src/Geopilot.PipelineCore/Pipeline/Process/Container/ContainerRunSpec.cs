namespace Geopilot.PipelineCore.Pipeline.Process.Container;

/// <summary>
/// Describes a one-shot container invocation.
/// </summary>
/// <param name="Image">Fully qualified image reference, e.g. "ghcr.io/myorg/mytool:1.2.3".</param>
/// <param name="Command">Command and arguments to run inside the container. If empty, the image's entrypoint/cmd is used.</param>
/// <param name="VolumeMounts">Bind-mounts as (hostPath, containerPath) pairs. Host paths must exist. Container paths must be absolute.</param>
/// <param name="Environment">Environment variables to set in the container.</param>
/// <param name="Timeout">Hard timeout for the container run. If elapsed, the container is killed. Null uses the runner's default.</param>
public sealed record ContainerRunSpec(
    string Image,
    IReadOnlyList<string> Command,
    IReadOnlyList<(string HostPath, string ContainerPath)> VolumeMounts,
    IReadOnlyDictionary<string, string>? Environment = null,
    TimeSpan? Timeout = null);
