using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Processes.Matcher.FileMatcher;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class CloudPipelineFileTest
{
    private const string CloudKey = "uploads/upload/data.xtf";
    private const string Content = "transfer file content";

    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private string materializationDirectory;

    [TestInitialize]
    public void Initialize()
    {
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        materializationDirectory = Path.Combine(Path.GetTempPath(), "CloudPipelineFile_" + Guid.NewGuid());
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(materializationDirectory))
            Directory.Delete(materializationDirectory, recursive: true);
    }

    [TestMethod]
    public void MetadataIsAvailableWithoutTransferringAnything()
    {
        var file = CreateFile();

        Assert.AreEqual("data.xtf", file.OriginalFileName);
        Assert.AreEqual("data", file.OriginalFileNameWithoutExtension);
        Assert.AreEqual("xtf", file.FileExtension);
        Assert.AreEqual(string.Empty, file.OriginalRelativePath);

        // A matcher that filters on metadata alone must not cause a download; the strict mock has no
        // setup, so any call to the store would have thrown.
        Assert.IsFalse(Directory.Exists(materializationDirectory));
    }

    [TestMethod]
    public async Task OpenReadFetchesTheFileOnFirstAccess()
    {
        SetupDownload(Content);
        var file = CreateFile();

        using (var reader = new StreamReader(await file.OpenReadAsync()))
        {
            Assert.AreEqual(Content, reader.ReadToEnd());
        }

        cloudStorageServiceMock.Verify(s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RepeatedAccessFetchesTheFileOnlyOnce()
    {
        SetupDownload(Content);
        var file = CreateFile();

        using (await file.OpenReadAsync())
        {
        }

        var path = await file.GetLocalPathAsync();
        using (await file.OpenReadAsync())
        {
        }

        cloudStorageServiceMock.Verify(s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(Content, File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(materializationDirectory));
    }

    [TestMethod]
    public async Task ConcurrentAccessFetchesTheFileOnlyOnce()
    {
        SetupDownload(Content);
        var file = CreateFile();

        var paths = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => file.GetLocalPathAsync())));

        Assert.HasCount(1, paths.Distinct().ToList());
        cloudStorageServiceMock.Verify(s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AFailedFetchLeavesNoPartialFileBehind()
    {
        cloudStorageServiceMock
            .Setup(s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (string key, Stream destination, CancellationToken cancellationToken) =>
            {
                await destination.WriteAsync(Encoding.UTF8.GetBytes("half"), cancellationToken);
                throw new IOException("connection reset");
            });

        var file = CreateFile();

        await Assert.ThrowsExactlyAsync<IOException>(() => file.OpenReadAsync());
        Assert.IsEmpty(Directory.GetFiles(materializationDirectory), "a partial file would look complete to the next reader");
    }

    [TestMethod]
    public async Task AFailedFetchIsNotRetried()
    {
        cloudStorageServiceMock
            .Setup(s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("connection reset"));

        var file = CreateFile();

        await Assert.ThrowsExactlyAsync<IOException>(() => file.OpenReadAsync());
        await Assert.ThrowsExactlyAsync<IOException>(() => file.GetLocalPathAsync());

        // The step that needs the file fails instead of silently retrying against a store that is down.
        cloudStorageServiceMock.Verify(s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void OpenWriteThrows()
    {
        var file = CreateFile();

        Assert.ThrowsExactly<NotSupportedException>(() => file.OpenWriteFileStream());
    }

    [TestMethod]
    public async Task TwoStepsConsumingTheSameUploadFetchItOnce()
    {
        SetupDownload(Content);

        var pipelineDirectory = Path.Combine(Path.GetTempPath(), "CloudPipelineFilePipeline_" + Guid.NewGuid());
        var reading = new UploadReadingProcess(takeLocalPath: false);
        var mutating = new UploadReadingProcess(takeLocalPath: true);
        var readingStep = BuildUploadStep("step_reading", pipelineDirectory, reading);
        var mutatingStep = BuildUploadStep("step_mutating", pipelineDirectory, mutating);

        using var pipeline = Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("two_step_pipeline")
            .DisplayName(LocalizedText.Empty)
            .Steps([readingStep, mutatingStep])
            .Logger(Mock.Of<ILogger>())
            .PipelineDirectory(pipelineDirectory)
            .JobId(Guid.NewGuid())
            .Build();

        await pipeline.Run([CreateFile()], CancellationToken.None);

        Assert.AreEqual(StepState.Success, readingStep.State);
        Assert.AreEqual(StepState.Success, mutatingStep.State);
        Assert.AreEqual(Content, reading.Contents.Single());
        Assert.AreEqual(Content, mutating.Contents.Single());

        cloudStorageServiceMock.Verify(
            s => s.DownloadAsync(CloudKey, It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "an upload file wired into two steps must be fetched from the store once");

        Assert.HasCount(1, Directory.GetFiles(materializationDirectory), "both steps share the one fetched file");
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(pipelineDirectory, "step_mutating")), "the step that asked for a local path gets its own private copy");
        Assert.IsFalse(Directory.Exists(Path.Combine(pipelineDirectory, "step_reading")), "reading alone must not copy");
    }

    [TestMethod]
    public async Task AFileTheMatcherFiltersOutIsNotFetchedDuringTheRun()
    {
        // Only the XTF may be fetched. The storage mock is strict and has no other setup, so a fetch of
        // any of the four other files would throw rather than quietly inflate the transfer volume.
        SetupDownload("data.xtf", Content);

        var upload = new IPipelineFile[]
        {
            CreateFile("readme.pdf"),
            CreateFile("sketch.png"),
            CreateFile("data.xtf"),
            CreateFile("notes.txt"),
            CreateFile("extra.zip"),
        };

        var pipelineDirectory = Path.Combine(Path.GetTempPath(), "CloudPipelineFileMatcher_" + Guid.NewGuid());
        var reading = new UploadReadingProcess(takeLocalPath: false);
        var matcherStep = BuildStep("matcher", pipelineDirectory, new FileMatcherProcess(new HashSet<string> { "xtf" }, null), new InputValue.UploadReference());
        var readerStep = BuildStep("reader", pipelineDirectory, reading, new InputValue.StepOutputReference("matcher", "MatchedFiles"));

        using var pipeline = Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("matcher_pipeline")
            .DisplayName(LocalizedText.Empty)
            .Steps([matcherStep, readerStep])
            .Logger(Mock.Of<ILogger>())
            .PipelineDirectory(pipelineDirectory)
            .JobId(Guid.NewGuid())
            .Build();

        await pipeline.Run(upload, CancellationToken.None);

        Assert.AreEqual(StepState.Success, matcherStep.State);
        Assert.AreEqual(StepState.Success, readerStep.State);
        Assert.AreEqual(Content, reading.Contents.Single(), "the reader saw exactly the matched file");

        cloudStorageServiceMock.Verify(
            s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "one of five uploaded files was read, so exactly one may be fetched while the pipeline runs");
        Assert.HasCount(1, Directory.GetFiles(materializationDirectory));
    }

    private PipelineStep BuildUploadStep(string id, string pipelineDirectory, object process)
        => BuildStep(id, pipelineDirectory, process, new InputValue.UploadReference());

    private PipelineStep BuildStep(string id, string pipelineDirectory, object process, InputValue filesInput) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue> { { "files", filesInput } })
            .OutputActions([])
            .Process(process)
            .PipelineDirectory(pipelineDirectory)
            .Logger(Mock.Of<ILogger>())
            .Build();

    private CloudPipelineFile CreateFile(string fileName = "data.xtf")
        => new CloudPipelineFile(cloudStorageServiceMock.Object, CloudKeyFor(fileName), fileName, materializationDirectory, fileName);

    private static string CloudKeyFor(string fileName) => $"uploads/upload/{fileName}";

    private void SetupDownload(string content) => SetupDownload("data.xtf", content);

    private void SetupDownload(string fileName, string content)
        => cloudStorageServiceMock
            .Setup(s => s.DownloadAsync(CloudKeyFor(fileName), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((string key, Stream destination, CancellationToken cancellationToken)
                => destination.WriteAsync(Encoding.UTF8.GetBytes(content), cancellationToken).AsTask());

    /// <summary>
    /// Reads every file it receives, either through the read stream or through a local path. Both are
    /// wired to <c>${upload()}</c> in the test, so the two variants model the two ways a process can
    /// consume the same uploaded file.
    /// </summary>
    private sealed class UploadReadingProcess
    {
        private readonly bool takeLocalPath;
        private readonly List<string> contents = new();

        public UploadReadingProcess(bool takeLocalPath) => this.takeLocalPath = takeLocalPath;

        public IReadOnlyList<string> Contents => contents;

        [PipelineProcessRun]
        public async Task<UploadReadingResult> RunAsync(IPipelineFile[] files, CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                if (takeLocalPath)
                {
                    contents.Add(await File.ReadAllTextAsync(await file.GetLocalPathAsync(cancellationToken), cancellationToken));
                }
                else
                {
                    using var stream = await file.OpenReadAsync(cancellationToken);
                    using var reader = new StreamReader(stream);
                    contents.Add(await reader.ReadToEndAsync(cancellationToken));
                }
            }

            return new UploadReadingResult { FileCount = files.Length };
        }
    }

    private sealed class UploadReadingResult
    {
        public int FileCount { get; init; }
    }
}
