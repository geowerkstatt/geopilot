using Geopilot.Api.Pipeline.Process.Container;
using Geopilot.PipelineCore.Pipeline.Process.Container;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Pipeline.Process.Container;

/// <summary>
/// Integration tests that require a working Docker daemon. Filter out with
/// <c>dotnet test --filter "TestCategory!=RequiresDocker"</c> in CI environments without Docker.
/// </summary>
[TestClass]
[TestCategory("RequiresDocker")]
public class DockerContainerRunnerTest
{
    private static readonly string SocketUri =
        Environment.OSVersion.Platform == PlatformID.Win32NT
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

    [TestMethod]
    public async Task RunAlpineEchoReturnsStdoutAndExitZero()
    {
        using var runner = CreateRunner(allowed: new List<string> { "alpine:*" });

        var spec = new ContainerRunSpec(
            "alpine:3",
            new List<string> { "echo", "hello from container" },
            Array.Empty<(string, string)>());

        var result = await runner.RunAsync(spec, CancellationToken.None);

        Assert.AreEqual(0L, result.ExitCode);
        StringAssert.Contains(result.Stdout, "hello from container");
    }

    [TestMethod]
    public async Task RunAlpineNonZeroExitIsReportedInResult()
    {
        using var runner = CreateRunner(allowed: new List<string> { "alpine:*" });

        var spec = new ContainerRunSpec(
            "alpine:3",
            new List<string> { "sh", "-c", "exit 42" },
            Array.Empty<(string, string)>());

        var result = await runner.RunAsync(spec, CancellationToken.None);

        Assert.AreEqual(42L, result.ExitCode);
    }

    [TestMethod]
    public async Task RunRejectsImageNotInAllowList()
    {
        using var runner = CreateRunner(allowed: new List<string> { "alpine:*" });

        var spec = new ContainerRunSpec(
            "busybox:latest",
            new List<string> { "echo", "nope" },
            Array.Empty<(string, string)>());

        await Assert.ThrowsAsync<ContainerRunException>(() => runner.RunAsync(spec, CancellationToken.None));
    }

    [TestMethod]
    public async Task RunWithBindMountReadsInputFromHost()
    {
        var hostDir = Directory.CreateTempSubdirectory("docker-runner-test-");
        try
        {
            var inputPath = Path.Combine(hostDir.FullName, "in.txt");
            await File.WriteAllTextAsync(inputPath, "payload\n");

            using var runner = CreateRunner(allowed: new List<string> { "alpine:*" });
            var spec = new ContainerRunSpec(
                "alpine:3",
                new List<string> { "cat", "/data/in.txt" },
                new List<(string HostPath, string ContainerPath)> { (hostDir.FullName, "/data") });

            var result = await runner.RunAsync(spec, CancellationToken.None);

            Assert.AreEqual(0L, result.ExitCode);
            StringAssert.Contains(result.Stdout, "payload");
        }
        finally
        {
            hostDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task RunRespectsTimeoutAndReportsFailure()
    {
        using var runner = CreateRunner(allowed: new List<string> { "alpine:*" });

        var spec = new ContainerRunSpec(
            "alpine:3",
            new List<string> { "sleep", "60" },
            Array.Empty<(string, string)>(),
            Timeout: TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<ContainerRunException>(() => runner.RunAsync(spec, CancellationToken.None));
    }

    private static DockerContainerRunner CreateRunner(List<string> allowed)
    {
        var options = new DockerRunnerOptions
        {
            SocketUri = SocketUri,
            AllowedImages = allowed,
            PullPolicy = DockerImagePullPolicy.IfNotPresent,
            DefaultTimeout = TimeSpan.FromMinutes(2),
            StopGracePeriod = TimeSpan.FromSeconds(2),
        };
        var logger = Mock.Of<ILogger<DockerContainerRunner>>();
        return new DockerContainerRunner(Options.Create(options), logger);
    }
}
