using Geopilot.Api.Pipeline.Process.XtfDiff;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class XtfDiffProcessTest
{
    private string testDirectory = null!;
    private string jobsDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "XtfDiffProcess_" + Guid.NewGuid());
        jobsDirectory = Path.Combine(testDirectory, "jobs");
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public async Task SunnyDay()
    {
        var process = CreateProcess();
        var inputFiles = new IPipelineFile[]
        {
            CreateXtfFile("data_2025.xtf", "<TRANSFER>new</TRANSFER>"),
            CreateXtfFile("data_2024.xtf", "<TRANSFER>old</TRANSFER>"),
            CreateXtfFile("readme.txt", "ignored"),
        };

        // Simulates the xtf-diff-worker: consume the job once input.ready appears and
        // answer with the diff fixture plus the output.ready sentinel.
        var fakeWorker = RunFakeWorkerAsync(jobDirectory =>
        {
            // The alphabetically first file is sent as the old state, the other as the new state.
            Assert.AreEqual("<TRANSFER>old</TRANSFER>", File.ReadAllText(Path.Combine(jobDirectory, "old.xtf")));
            Assert.AreEqual("<TRANSFER>new</TRANSFER>", File.ReadAllText(Path.Combine(jobDirectory, "new.xtf")));
            Assert.IsFalse(File.Exists(Path.Combine(jobDirectory, "args.json")), "args.json is only written when a model directory is configured.");
            File.Copy("TestData/XtfDiff/diff.json", Path.Combine(jobDirectory, "diff.json"));
        });

        // Await the fake worker first: if one of its assertions fails, the process would
        // otherwise poll for the output sentinel until the timeout token cancels it.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var runTask = process.RunAsync(inputFiles, timeout.Token);
        await fakeWorker.ConfigureAwait(false);
        var processResult = await runTask.ConfigureAwait(false);

        Assert.IsNotNull(processResult);
        Assert.HasCount(3, processResult);

        var diffFile = processResult["diff_file"] as PipelineFile;
        Assert.IsNotNull(diffFile);
        using (var diffStream = diffFile.OpenReadFileStream())
        {
            var diffEntries = JsonSerializer.Deserialize<List<XtfDiffEntry>>(diffStream);
            Assert.IsNotNull(diffEntries);
            Assert.HasCount(6, diffEntries);
        }

        var configFile = processResult["map_visualization_config_file"] as PipelineFile;
        Assert.IsNotNull(configFile);
        using (var configStream = configFile.OpenReadFileStream())
        {
            using var config = JsonDocument.Parse(configStream);
            Assert.AreEqual(5, config.RootElement.GetProperty("layers").GetArrayLength());
        }

        var statusMessage = processResult["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(statusMessage);
        Assert.HasCount(4, statusMessage);
        Assert.AreEqual("Compared 'data_2024.xtf' (old) with 'data_2025.xtf' (new): 2 geometry(ies) added, 1 deleted, 1 changed.", statusMessage["en"]);

        // The job folder is cleaned up after the result has been consumed.
        Assert.IsEmpty(Directory.GetDirectories(jobsDirectory));
    }

    [TestMethod]
    public async Task FailedToolRunThrowsWithErrorLog()
    {
        var process = CreateProcess();
        var inputFiles = new IPipelineFile[]
        {
            CreateXtfFile("a.xtf", "<TRANSFER>a</TRANSFER>"),
            CreateXtfFile("b.xtf", "<TRANSFER>b</TRANSFER>"),
        };

        var fakeWorker = RunFakeWorkerAsync(jobDirectory =>
            File.WriteAllText(Path.Combine(jobDirectory, "error.log"), "XTF-Diff-Tool failed (exit_code=1)"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(inputFiles, timeout.Token));
        await fakeWorker.ConfigureAwait(false);
        StringAssert.Contains(exception.Message, "XTF-Diff-Tool failed (exit_code=1)");

        // The job folder is cleaned up on failure as well.
        Assert.IsEmpty(Directory.GetDirectories(jobsDirectory));
    }

    [TestMethod]
    public async Task ThrowsWithoutTwoXtfFiles()
    {
        var process = CreateProcess();
        var inputFiles = new IPipelineFile[]
        {
            CreateXtfFile("single.xtf", "<TRANSFER>single</TRANSFER>"),
            CreateXtfFile("readme.txt", "ignored"),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(inputFiles, CancellationToken.None));
        StringAssert.Contains(exception.Message, "exactly two XTF files");
    }

    private XtfDiffProcess CreateProcess()
    {
        var pipelineFileManagerMock = new Mock<IPipelineFileManager>();
        pipelineFileManagerMock.Setup(m => m.GeneratePipelineFile(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string originalFileName, string fileExtension) =>
            {
                var filePath = Path.Combine(testDirectory, $"{originalFileName}_{Guid.NewGuid()}.{fileExtension}");
                return new PipelineFile(filePath, originalFileName + "." + fileExtension);
            });

        return new XtfDiffProcess(
            jobsDirectory: jobsDirectory,
            pollInterval: 25,
            modelDirectory: null,
            baseMapWmtsCapabilitiesUrl: null,
            pipelineFileManager: pipelineFileManagerMock.Object,
            logger: Mock.Of<ILogger<XtfDiffProcessTest>>());
    }

    private PipelineFile CreateXtfFile(string fileName, string content)
    {
        var filePath = Path.Combine(testDirectory, fileName);
        File.WriteAllText(filePath, content);
        return new PipelineFile(filePath, fileName);
    }

    /// <summary>
    /// Simulates the xtf-diff-worker side of the file-drop protocol: waits for a job folder
    /// containing an <c>input.ready</c> sentinel, lets <paramref name="produceOutput"/> write the
    /// job result, and drops the <c>output.ready</c> sentinel last.
    /// </summary>
    private Task RunFakeWorkerAsync(Action<string> produceOutput)
    {
        return Task.Run(async () =>
        {
            for (var i = 0; i < 400; i++)
            {
                if (Directory.Exists(jobsDirectory))
                {
                    var inputReady = Directory.GetFiles(jobsDirectory, "input.ready", SearchOption.AllDirectories).FirstOrDefault();
                    if (inputReady != null)
                    {
                        var jobDirectory = Path.GetDirectoryName(inputReady)!;
                        produceOutput(jobDirectory);
                        await File.WriteAllBytesAsync(Path.Combine(jobDirectory, "output.ready"), Array.Empty<byte>());
                        return;
                    }
                }

                await Task.Delay(25);
            }

            throw new TimeoutException("The fake worker never observed an input.ready sentinel.");
        });
    }
}
