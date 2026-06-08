using Geopilot.Api.Pipeline.Process.Hop;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class HopClientTest
{
    private string testDirectory = null!;
    private string jobsDirectory = null!;
    private PipelineFileManager fileManager = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "HopClient_" + Guid.NewGuid());
        Directory.CreateDirectory(testDirectory);
        jobsDirectory = Path.Combine(testDirectory, "jobs");
        fileManager = new PipelineFileManager(Path.Combine(testDirectory, "output"), "HopClient");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public async Task SuccessWritesInputsAndCollectsOutputs()
    {
        var client = CreateClient();
        var inputs = new[]
        {
            CreateInputFile("data.xtf", "<xtf/>"),
            CreateInputFile("meta.csv", "a;b"),
        };
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal) { { "result.gpkg", "RESULT" } };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, outputs, "done", cts.Token);
        var result = await client.RunAsync(inputs, NewArgs(), cts.Token);
        var observation = await workerTask;

        Assert.IsTrue(result.Success);
        Assert.AreEqual("done", result.Log);
        Assert.HasCount(1, result.OutputFiles);

        var outputFile = result.OutputFiles[0];
        Assert.AreEqual("result.gpkg", outputFile.OriginalFileName);
        Assert.AreEqual(string.Empty, outputFile.OriginalRelativePath);
        using (var reader = new StreamReader(outputFile.OpenReadFileStream()))
        {
            Assert.AreEqual("RESULT", reader.ReadToEnd());
        }

        CollectionAssert.AreEquivalent(new[] { "data.xtf", "meta.csv" }, observation.InputFiles.Keys.ToArray());
        Assert.AreEqual("<xtf/>", observation.InputFiles["data.xtf"]);
    }

    [TestMethod]
    public async Task ArgsJsonContainsPipelineAndParameters()
    {
        var client = CreateClient();
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "SOURCE_SRS", "2056" },
            { "TARGET_FORMAT", "gpkg" },
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, NoOutputs(), "ok", cts.Token);
        await client.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, new HopArgs("transform_xtf.hpl", parameters), cts.Token);
        var observation = await workerTask;

        using var document = JsonDocument.Parse(observation.ArgsJson);
        var root = document.RootElement;
        Assert.AreEqual("transform_xtf.hpl", root.GetProperty("pipeline").GetString());
        var parametersElement = root.GetProperty("parameters");
        Assert.AreEqual("2056", parametersElement.GetProperty("SOURCE_SRS").GetString());
        Assert.AreEqual("gpkg", parametersElement.GetProperty("TARGET_FORMAT").GetString());
    }

    [TestMethod]
    public async Task EmptyParametersAreOmittedFromArgsJson()
    {
        var client = CreateClient();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, NoOutputs(), "ok", cts.Token);
        await client.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, NewArgs(), cts.Token);
        var observation = await workerTask;

        using var document = JsonDocument.Parse(observation.ArgsJson);
        Assert.IsFalse(document.RootElement.TryGetProperty("parameters", out _));
    }

    [TestMethod]
    public async Task NestedRelativePathsArePreserved()
    {
        var client = CreateClient();
        var inputs = new[]
        {
            CreateInputFile("deep.xtf", "deep", "a/b"),
            CreateInputFile("root.xtf", "root"),
        };
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "reports/summary.txt", "S" },
            { "result.gpkg", "R" },
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, outputs, "ok", cts.Token);
        var result = await client.RunAsync(inputs, NewArgs(), cts.Token);
        var observation = await workerTask;

        CollectionAssert.AreEquivalent(new[] { "a/b/deep.xtf", "root.xtf" }, observation.InputFiles.Keys.ToArray());

        var summary = result.OutputFiles.Single(file => file.OriginalFileName == "summary.txt");
        Assert.AreEqual("reports", summary.OriginalRelativePath);
        var gpkg = result.OutputFiles.Single(file => file.OriginalFileName == "result.gpkg");
        Assert.AreEqual(string.Empty, gpkg.OriginalRelativePath);
    }

    [TestMethod]
    public async Task FailureReturnsErrorLogAndNoOutputs()
    {
        var client = CreateClient();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: false, NoOutputs(), "boom", cts.Token);
        var result = await client.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, NewArgs(), cts.Token);
        await workerTask;

        Assert.IsFalse(result.Success);
        Assert.AreEqual("boom", result.Log);
        Assert.HasCount(0, result.OutputFiles);
    }

    [TestMethod]
    public async Task CompletionWithoutLogIsTreatedAsFailure()
    {
        var client = CreateClient();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: null, NoOutputs(), string.Empty, cts.Token);
        var result = await client.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, NewArgs(), cts.Token);
        await workerTask;

        Assert.IsFalse(result.Success);
        Assert.HasCount(0, result.OutputFiles);
        StringAssert.Contains(result.Log, "neither");
    }

    [TestMethod]
    public async Task JobDirectoryIsCleanedUpAfterRun()
    {
        var client = CreateClient();
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal) { { "result.gpkg", "R" } };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, outputs, "ok", cts.Token);
        await client.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, NewArgs(), cts.Token);
        await workerTask;

        Assert.HasCount(0, Directory.GetDirectories(jobsDirectory));
    }

    [TestMethod]
    public async Task TimesOutWhenWorkerNeverResponds()
    {
        var client = CreateClient(timeout: TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, NewArgs()));
    }

    private static HopArgs NewArgs() => new("transform_xtf.hpl", new Dictionary<string, string>(StringComparer.Ordinal));

    private static Dictionary<string, string> NoOutputs() => new(StringComparer.Ordinal);

    private HopClient CreateClient(TimeSpan? timeout = null) =>
        new(
            new HopClientOptions
            {
                JobsDirectory = jobsDirectory,
                PollInterval = TimeSpan.FromMilliseconds(40),
                Timeout = timeout ?? TimeSpan.FromSeconds(20),
            },
            fileManager,
            Mock.Of<ILogger<HopClientTest>>());

    private PipelineFile CreateInputFile(string fileName, string content, string? relativePath = null)
    {
        var path = Path.Combine(testDirectory, "in_" + Guid.NewGuid() + "_" + fileName);
        File.WriteAllText(path, content);
        return relativePath is null
            ? new PipelineFile(path, fileName)
            : new PipelineFile(path, fileName, relativePath);
    }
}
