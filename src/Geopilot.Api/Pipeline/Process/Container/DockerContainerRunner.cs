using Docker.DotNet;
using Docker.DotNet.Models;
using Geopilot.PipelineCore.Pipeline.Process.Container;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline.Process.Container;

/// <summary>
/// <see cref="IContainerRunner"/> implementation backed by the Docker Engine API via Docker.DotNet.
/// </summary>
/// <remarks>
/// The runner talks to the Docker daemon through the socket configured in <see cref="DockerRunnerOptions.SocketUri"/>.
/// When GeoPilot runs inside a container, the host's docker socket must be bind-mounted into the container.
/// This grants root-equivalent access to the host; the image allow-list in <see cref="DockerRunnerOptions.AllowedImages"/>
/// is the primary security guard and is required.
/// </remarks>
public class DockerContainerRunner : IContainerRunner, IDisposable
{
    private readonly DockerRunnerOptions options;
    private readonly ILogger<DockerContainerRunner> logger;
    private readonly DockerClient dockerClient;
    private readonly List<Regex> allowedImagePatterns;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerContainerRunner"/> class.
    /// </summary>
    /// <param name="options">Docker runner options.</param>
    /// <param name="logger">Logger instance.</param>
    public DockerContainerRunner(IOptions<DockerRunnerOptions> options, ILogger<DockerContainerRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;
        this.logger = logger;
        using (var clientConfig = new DockerClientConfiguration(new Uri(this.options.SocketUri)))
        {
            this.dockerClient = (DockerClient)clientConfig.CreateClient();
        }

        this.allowedImagePatterns = this.options.AllowedImages
            .Select(GlobToRegex)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ContainerRunResult> RunAsync(ContainerRunSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!IsImageAllowed(spec.Image))
        {
            throw new ContainerRunException(
                $"Image '{spec.Image}' is not permitted. Add a matching pattern to Docker:AllowedImages in configuration.");
        }

        await EnsureImagePulledAsync(spec.Image, cancellationToken);

        var timeout = spec.Timeout ?? options.DefaultTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var createResponse = await CreateContainerAsync(spec, timeoutCts.Token);
        var containerId = createResponse.ID;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var started = await dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), timeoutCts.Token);
            if (!started)
                throw new ContainerRunException($"Failed to start container '{containerId}' for image '{spec.Image}'.");

            logger.LogInformation("Started container {ContainerId} from image '{Image}' with timeout {Timeout}.", containerId, spec.Image, timeout);

            var stdoutBuffer = new StringBuilder();
            var stderrBuffer = new StringBuilder();

            // Start log streaming in parallel with the wait.
            var logTask = StreamLogsAsync(containerId, stdoutBuffer, stderrBuffer, timeoutCts.Token);

            ContainerWaitResponse waitResponse;
            try
            {
                waitResponse = await dockerClient.Containers.WaitContainerAsync(containerId, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.LogError("Container {ContainerId} timed out after {Timeout}; stopping.", containerId, timeout);
                await TryStopContainerAsync(containerId);
                throw new ContainerRunException($"Container '{spec.Image}' timed out after {timeout}.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogError("Container {ContainerId} cancelled by caller; stopping.", containerId);
                await TryStopContainerAsync(containerId);
                throw;
            }

            // Wait for the log stream to drain fully after the container exits so we don't
            // truncate trailing error messages. The task completes when Docker signals EOF on the
            // multiplexed log stream. We cap the wait at 10s to avoid hanging if the daemon is slow
            // to close the stream.
            await Task.WhenAny(logTask, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None));

            stopwatch.Stop();
            logger.LogInformation(
                "Container {ContainerId} exited with code {ExitCode} after {Duration}.",
                containerId,
                waitResponse.StatusCode,
                stopwatch.Elapsed);

            return new ContainerRunResult(
                waitResponse.StatusCode,
                stdoutBuffer.ToString(),
                stderrBuffer.ToString(),
                stopwatch.Elapsed);
        }
        finally
        {
            await TryRemoveContainerAsync(containerId);
        }
    }

    /// <summary>
    /// Disposes the underlying Docker client.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources held by this instance.
    /// </summary>
    /// <param name="disposing">True if called from <see cref="Dispose()"/>; false if called from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
            dockerClient.Dispose();

        disposed = true;
    }

    private bool IsImageAllowed(string image)
    {
        if (allowedImagePatterns.Count == 0)
            return false;

        return allowedImagePatterns.Any(pattern => pattern.IsMatch(image));
    }

    private static Regex GlobToRegex(string glob)
    {
        // Convert a shell-style glob (only '*' supported) into an anchored regex.
        var escaped = Regex.Escape(glob).Replace("\\*", ".*", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private async Task EnsureImagePulledAsync(string image, CancellationToken ct)
    {
        if (options.PullPolicy == DockerImagePullPolicy.Never)
            return;

        if (options.PullPolicy == DockerImagePullPolicy.IfNotPresent)
        {
            var listParams = new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["reference"] = new Dictionary<string, bool> { [image] = true },
                },
            };
            var existing = await dockerClient.Images.ListImagesAsync(listParams, ct);
            if (existing.Count > 0)
                return;
        }

        logger.LogInformation("Pulling image '{Image}' (policy={Policy}).", image, options.PullPolicy);
        var (fromImage, tag) = SplitImage(image);
        var createParams = new ImagesCreateParameters { FromImage = fromImage, Tag = tag };
        var progress = new Progress<JSONMessage>(_ => { });
        await dockerClient.Images.CreateImageAsync(createParams, null, progress, ct);
    }

    private static (string FromImage, string Tag) SplitImage(string image)
    {
        // Support "repo:tag" and "repo@digest". Digest form: pass the full reference as FromImage with tag=null.
        if (image.Contains('@', StringComparison.Ordinal))
            return (image, string.Empty);

        var lastColon = image.LastIndexOf(':');
        var lastSlash = image.LastIndexOf('/');
        if (lastColon > lastSlash && lastColon >= 0)
            return (image[..lastColon], image[(lastColon + 1)..]);

        return (image, "latest");
    }

    private async Task<CreateContainerResponse> CreateContainerAsync(ContainerRunSpec spec, CancellationToken ct)
    {
        // Docker rejects relative host paths in bind mounts — a non-rooted path is interpreted as a
        // named volume identifier instead. Resolve to an absolute path (relative to the current
        // working directory) so callers can use relative paths in configuration without surprises.
        var binds = spec.VolumeMounts
            .Select(m => $"{Path.GetFullPath(m.HostPath)}:{m.ContainerPath}")
            .ToList();

        var env = spec.Environment?
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList();

        var parameters = new CreateContainerParameters
        {
            Image = spec.Image,
            Cmd = spec.Command.ToList(),
            Env = env,
            User = options.ContainerUser,
            AttachStdout = true,
            AttachStderr = true,
            Tty = false,
            HostConfig = new HostConfig
            {
                Binds = binds,
                AutoRemove = false, // we remove in finally to ensure logs are captured before removal
                Privileged = false,
                NetworkMode = "bridge",
            },
        };

        return await dockerClient.Containers.CreateContainerAsync(parameters, ct);
    }

    private async Task StreamLogsAsync(string containerId, StringBuilder stdout, StringBuilder stderr, CancellationToken ct)
    {
        try
        {
            var logParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = true,
                Timestamps = false,
            };
            using var stream = await dockerClient.Containers.GetContainerLogsAsync(containerId, false, logParams, ct);

            var buffer = new byte[8192];
            while (!ct.IsCancellationRequested)
            {
                var read = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
                if (read.EOF)
                    return;

                var text = Encoding.UTF8.GetString(buffer, 0, read.Count);
                switch (read.Target)
                {
                    case MultiplexedStream.TargetStream.StandardOut:
                        stdout.Append(text);
                        logger.LogInformation("[container {ContainerId}] stdout: {Text}", containerId, text.TrimEnd());
                        break;
                    case MultiplexedStream.TargetStream.StandardError:
                        stderr.Append(text);
                        logger.LogError("[container {ContainerId}] stderr: {Text}", containerId, text.TrimEnd());
                        break;
                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancel/shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading logs from container {ContainerId}.", containerId);
        }
    }

    private async Task TryStopContainerAsync(string containerId)
    {
        try
        {
            await dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = (uint)options.StopGracePeriod.TotalSeconds },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop container {ContainerId}; will force-remove.", containerId);
        }
    }

    private async Task TryRemoveContainerAsync(string containerId)
    {
        try
        {
            await dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = true },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove container {ContainerId}.", containerId);
        }
    }
}
