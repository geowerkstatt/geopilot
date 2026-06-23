using Geopilot.Pipeline;
using Geopilot.Pipeline.Processes.Hop;
using Geopilot.Pipeline.Processes.Matcher.FileRenamer;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class HopProcessTest
{
    private string testDirectory = null!;
    private string jobsDirectory = null!;
    private PipelineFileManager fileManager = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "HopProcess_" + Guid.NewGuid());
        Directory.CreateDirectory(testDirectory);
        jobsDirectory = Path.Combine(testDirectory, "jobs");
        fileManager = new PipelineFileManager(Path.Combine(testDirectory, "output"), "HopProcess");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public async Task SuccessProducesOutputFilesAndStatusMessage()
    {
        var process = CreateProcess();
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "result.gpkg", "R" },
            { "report.csv", "C" },
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, outputs, "ok", cts.Token);
        var result = await process.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, cts.Token);
        await workerTask;

        var outputFiles = result["output_files"] as IPipelineFile[];
        Assert.IsNotNull(outputFiles);
        Assert.HasCount(2, outputFiles);

        var statusMessage = result["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(statusMessage);
        var expected = new Dictionary<string, string>
        {
            { "de", "Hop-Pipeline erfolgreich ausgeführt, 2 Datei(en) erzeugt." },
            { "fr", "Pipeline Hop exécuté avec succès, 2 fichier(s) généré(s)." },
            { "it", "Pipeline Hop eseguito correttamente, 2 file generati." },
            { "en", "Hop pipeline completed successfully, 2 file(s) produced." },
        };
        CollectionAssert.AreEqual(expected, statusMessage);
    }

    [TestMethod]
    public async Task FailureProducesEmptyOutputAndFailureStatusMessage()
    {
        var process = CreateProcess();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: false, new Dictionary<string, string>(StringComparer.Ordinal), "boom", cts.Token);
        var result = await process.RunAsync(new[] { CreateInputFile("data.xtf", "<xtf/>") }, cts.Token);
        await workerTask;

        var outputFiles = result["output_files"] as IPipelineFile[];
        Assert.IsNotNull(outputFiles);
        Assert.HasCount(0, outputFiles);

        var statusMessage = result["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(statusMessage);
        var expected = new Dictionary<string, string>
        {
            { "de", "Hop-Pipeline fehlgeschlagen." },
            { "fr", "Le pipeline Hop a échoué." },
            { "it", "Pipeline Hop non riuscito." },
            { "en", "Hop pipeline failed." },
        };
        CollectionAssert.AreEqual(expected, statusMessage);
    }

    [TestMethod]
    public void EmptyJobsDirectoryThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new HopProcess(string.Empty, "transform_xtf.hpl", null, null, new Dictionary<string, string>(), fileManager, Mock.Of<ILogger<HopProcessTest>>()));
    }

    [TestMethod]
    public void EmptyPipelineThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new HopProcess(jobsDirectory, string.Empty, null, null, new Dictionary<string, string>(), fileManager, Mock.Of<ILogger<HopProcessTest>>()));
    }

    [TestMethod]
    public async Task RenamerSubdirectoryTargetPlacesFileInWorkerInputSubdirectory()
    {
        // FileRenamer maps the upload to a target with a nested directory; the renamed file must
        // arrive under input/<dir>/ of the worker, proving the relative path survives FileRenamer -> Hop.
        var uploadFiles = new PipelineFileList(new List<IPipelineFile> { CreateInputFile("input.gpkg", "GPKGCONTENT") });
        var renamer = new FileRenamerProcess(
            new List<FileMapping> { new("^.*[.]gpkg$", "sub/dir/renamed.gpkg") },
            fileManager);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var renameResult = await renamer.RunAsync(uploadFiles, cts.Token);
        var renamedFiles = renameResult["renamed_files"] as List<IPipelineFile>;
        Assert.IsNotNull(renamedFiles);
        Assert.HasCount(1, renamedFiles);
        Assert.AreEqual("renamed.gpkg", renamedFiles[0].OriginalFileName);
        Assert.AreEqual("sub/dir", renamedFiles[0].OriginalRelativePath);

        var process = CreateProcess();
        var workerTask = HopWorkerSimulator.RunAsync(jobsDirectory, success: true, new Dictionary<string, string>(StringComparer.Ordinal), "ok", cts.Token);
        await process.RunAsync(renamedFiles.ToArray(), cts.Token);
        var observation = await workerTask;

        // The renamed file landed under its nested target directory (rename + relative path both honored),
        // with content intact through both copies (FileRenamer output -> HopClient mount).
        CollectionAssert.AreEquivalent(new[] { "sub/dir/renamed.gpkg" }, observation.InputFiles.Keys.ToArray());
        Assert.AreEqual("GPKGCONTENT", observation.InputFiles["sub/dir/renamed.gpkg"]);
    }

    private HopProcess CreateProcess(string pipeline = "transform_xtf.hpl") =>
        new(jobsDirectory, pipeline, timeoutSeconds: 20, pollInterval: 40, new Dictionary<string, string>(), fileManager, Mock.Of<ILogger<HopProcessTest>>());

    private PipelineFile CreateInputFile(string fileName, string content)
    {
        var path = Path.Combine(testDirectory, "in_" + Guid.NewGuid() + "_" + fileName);
        File.WriteAllText(path, content);
        return new PipelineFile(path, fileName);
    }
}
