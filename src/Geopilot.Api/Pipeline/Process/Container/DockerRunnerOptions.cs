using Geopilot.PipelineCore.Pipeline.Process.Container;

namespace Geopilot.Api.Pipeline.Process.Container;

/// <summary>
/// Configuration for <see cref="DockerContainerRunner"/>.
/// </summary>
public class DockerRunnerOptions
{
    /// <summary>
    /// Configuration section name in appsettings. Nested under <c>Pipeline</c> since the Docker
    /// runner is a pipeline concern (used by container-based pipeline processes).
    /// </summary>
    public const string SectionName = "Pipeline:Docker";

    /// <summary>
    /// URI of the Docker daemon socket. Defaults to the platform-native location:
    /// <c>npipe://./pipe/docker_engine</c> on Windows, <c>unix:///var/run/docker.sock</c> elsewhere.
    /// Explicit configuration overrides the default.
    /// </summary>
    public string SocketUri { get; set; } = DefaultSocketUri();

    private static string DefaultSocketUri() =>
        OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

    /// <summary>
    /// Image allow-list. Each entry is a glob pattern ('*' matches any substring, including empty).
    /// Examples: "ghcr.io/myorg/*", "interlis/check:*", "alpine:3".
    /// An empty list disables container runs entirely. Configuring this is required for security.
    /// </summary>
    public List<string> AllowedImages { get; set; } = new();

    /// <summary>
    /// Image pull policy.
    /// <c>IfNotPresent</c>: pull only when the image is not already on the host (default).
    /// <c>Always</c>: pull on every run.
    /// <c>Never</c>: never pull; fail if the image is missing.
    /// </summary>
    public DockerImagePullPolicy PullPolicy { get; set; } = DockerImagePullPolicy.IfNotPresent;

    /// <summary>
    /// Default timeout applied when <see cref="ContainerRunSpec.Timeout"/> is not set. Default is 10 minutes.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Optional user to run the container as, in Docker's "user[:group]" syntax (e.g. "1000:1000").
    /// Null leaves the image default user.
    /// </summary>
    public string? ContainerUser { get; set; }

    /// <summary>
    /// Grace period for SIGTERM before SIGKILL is sent on cancellation or timeout.
    /// </summary>
    public TimeSpan StopGracePeriod { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Controls when a container image is pulled from the registry.
/// </summary>
public enum DockerImagePullPolicy
{
    /// <summary>Pull the image only if it is not already present on the host.</summary>
    IfNotPresent,

    /// <summary>Pull the image on every run.</summary>
    Always,

    /// <summary>Never pull the image. Fail if the image is missing.</summary>
    Never,
}
