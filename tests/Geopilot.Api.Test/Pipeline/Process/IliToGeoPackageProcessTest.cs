using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.IliToGeoPackage;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

/// <summary>
/// Tests for the file-drop flow of <see cref="IliToGeoPackageProcess"/>. Each test spins up a
/// <see cref="FakeWorker"/> that watches the shared input directory for the
/// <c>{correlationId}.input.ready</c> sentinel, produces the requested success or error
/// payload, then writes a <c>{correlationId}.output.ready</c> sentinel last — mirroring
/// what the real ili2gpkg-worker container does in production.
/// </summary>
[TestClass]
public class IliToGeoPackageProcessTest
{
    private Mock<ILogger> loggerMock = null!;
    private string rootDir = null!;
    private string inputDir = null!;
    private string outputDir = null!;
    private string pipelineWorkDir = null!;
    private PipelineFileManager fileManager = null!;

    [TestInitialize]
    public void SetUp()
    {
        loggerMock = new Mock<ILogger>();
        rootDir = Path.Combine(Path.GetTempPath(), "ili2gpkg-test-" + Guid.NewGuid().ToString("N"));
        inputDir = Path.Combine(rootDir, "in");
        outputDir = Path.Combine(rootDir, "out");
        pipelineWorkDir = Path.Combine(rootDir, "pipeline");
        Directory.CreateDirectory(rootDir);
        fileManager = new PipelineFileManager(pipelineWorkDir, "step1");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(rootDir))
                Directory.Delete(rootDir, recursive: true);
        }
        catch (IOException)
        {
            // best effort — another process (e.g. antivirus scanner) may hold handles briefly.
        }
    }

    [TestMethod]
    public async Task RunAsyncReturnsProducedGpkgWhenWorkerWritesOutput()
    {
        var process = CreateProcess();
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01, 0x02, 0x03 });

        await using var worker = FakeWorker.Start(inputDir, outputDir, _ => FakeWorker.Gpkg(new byte[] { 0x53, 0x51, 0x4C }));

        var result = await process.RunAsync(new[] { input }, CancellationToken.None);

        var gpkgFiles = (IPipelineFile[])result["gpkg_files"]!;
        Assert.HasCount(1, gpkgFiles);
        Assert.AreEqual("roads", gpkgFiles[0].OriginalFileNameWithoutExtension);
        Assert.AreEqual("gpkg", gpkgFiles[0].FileExtension);

        // Contents of the pipeline-registered gpkg must match what the worker produced.
        using var stream = gpkgFiles[0].OpenReadFileStream();
        var bytes = new byte[stream.Length];
        await stream.ReadExactlyAsync(bytes);
        CollectionAssert.AreEqual(new byte[] { 0x53, 0x51, 0x4C }, bytes);
    }

    [TestMethod]
    public async Task RunAsyncDeletesOutputFileAfterConsumption()
    {
        var process = CreateProcess();
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        await using var worker = FakeWorker.Start(inputDir, outputDir, _ => FakeWorker.Gpkg(new byte[] { 0x53 }));

        await process.RunAsync(new[] { input }, CancellationToken.None);

        // Once geopilot has copied the gpkg into its pipeline file manager, both the
        // shared gpkg and its output.ready sentinel must be removed so the output dir
        // doesn't grow unbounded.
        Assert.HasCount(0, Directory.EnumerateFiles(outputDir).ToArray(), "Shared output directory should be empty after consumption.");
    }

    [TestMethod]
    public async Task RunAsyncReturnsSingularStatusMessageForOneFile()
    {
        var process = CreateProcess();
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        await using var worker = FakeWorker.Start(inputDir, outputDir, _ => FakeWorker.Gpkg(new byte[] { 0x53 }));

        var result = await process.RunAsync(new[] { input }, CancellationToken.None);

        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        Assert.AreEqual("1 GeoPackage created", statusMessage["en"]);
        Assert.AreEqual("1 GeoPackage erstellt", statusMessage["de"]);
        Assert.AreEqual("1 GeoPackage créé", statusMessage["fr"]);
        Assert.AreEqual("1 GeoPackage creato", statusMessage["it"]);
    }

    [TestMethod]
    public async Task RunAsyncReturnsPluralStatusMessageForMultipleFiles()
    {
        var process = CreateProcess();
        var input1 = WriteInputXtf("roads.xtf", new byte[] { 0x01 });
        var input2 = WriteInputXtf("buildings.xtf", new byte[] { 0x02 });

        await using var worker = FakeWorker.Start(inputDir, outputDir, _ => FakeWorker.Gpkg(new byte[] { 0x53 }));

        var result = await process.RunAsync(new[] { input1, input2 }, CancellationToken.None);

        var gpkgFiles = (IPipelineFile[])result["gpkg_files"]!;
        Assert.HasCount(2, gpkgFiles);

        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        Assert.AreEqual("2 GeoPackages created", statusMessage["en"]);
        Assert.AreEqual("2 GeoPackages erstellt", statusMessage["de"]);
        Assert.AreEqual("2 GeoPackages créés", statusMessage["fr"]);
        Assert.AreEqual("2 GeoPackages creati", statusMessage["it"]);
    }

    [TestMethod]
    public async Task RunAsyncThrowsInvalidOperationExceptionWhenWorkerReportsError()
    {
        var process = CreateProcess();
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        await using var worker = FakeWorker.Start(inputDir, outputDir, _ => FakeWorker.Error("model Foo_V1 not found"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(new[] { input }, CancellationToken.None));
        StringAssert.Contains(ex.Message, "roads.xtf");
        StringAssert.Contains(ex.Message, "model Foo_V1 not found");

        Assert.HasCount(0, Directory.EnumerateFiles(outputDir).ToArray(), "Error payload and output.ready sentinel should be removed after consumption.");
    }

    [TestMethod]
    public async Task RunAsyncThrowsTimeoutExceptionWhenNoOutputAppears()
    {
        // Short timeout — no worker — should time out quickly with a clear diagnostic.
        var process = CreateProcess(pollIntervalMs: 50, timeoutSeconds: 1);
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => process.RunAsync(new[] { input }, CancellationToken.None));
        StringAssert.Contains(ex.Message, "ili2gpkg-worker");
        StringAssert.Contains(ex.Message, "roads.xtf");
    }

    [TestMethod]
    public async Task RunAsyncWritesConfigSidecarWithExpectedFields()
    {
        var process = CreateProcess(
            import: false,
            disableValidation: true,
            createBasketCol: true,
            smart2Inheritance: true,
            schemaImport: true,
            models: new HashSet<string> { "RoadsExdm2ien", "Units" },
            defaultSrsCode: 2056);
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        // Fake worker captures the config it reads and then produces a gpkg. We capture a
        // JsonElement.Clone so the element survives after the worker disposes its parent
        // JsonDocument.
        JsonElement? capturedConfig = null;
        await using var worker = FakeWorker.Start(inputDir, outputDir, config =>
        {
            capturedConfig = config;
            return FakeWorker.Gpkg(new byte[] { 0x53 });
        });

        await process.RunAsync(new[] { input }, CancellationToken.None);

        Assert.IsNotNull(capturedConfig, "worker should have read the config sidecar");
        var root = capturedConfig!.Value;
        Assert.AreEqual("roads.xtf", root.GetProperty("originalFileName").GetString());
        Assert.IsFalse(root.GetProperty("import").GetBoolean());
        Assert.IsTrue(root.GetProperty("disableValidation").GetBoolean());
        Assert.IsTrue(root.GetProperty("createBasketCol").GetBoolean());
        Assert.IsTrue(root.GetProperty("smart2Inheritance").GetBoolean());
        Assert.IsTrue(root.GetProperty("schemaImport").GetBoolean());
        Assert.AreEqual(2056, root.GetProperty("defaultSrsCode").GetInt32());
        CollectionAssert.AreEquivalent(
            new[] { "RoadsExdm2ien", "Units" },
            root.GetProperty("models").EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [TestMethod]
    public async Task RunAsyncOmitsDefaultSrsCodeWhenNull()
    {
        var process = CreateProcess(defaultSrsCode: null);
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        JsonElement? capturedConfig = null;
        await using var worker = FakeWorker.Start(inputDir, outputDir, config =>
        {
            capturedConfig = config;
            return FakeWorker.Gpkg(new byte[] { 0x53 });
        });

        await process.RunAsync(new[] { input }, CancellationToken.None);

        Assert.IsNotNull(capturedConfig);

        // The JSON serializer is configured with DefaultIgnoreCondition=WhenWritingNull, so a
        // null defaultSrsCode must not appear in the emitted config.
        Assert.IsFalse(
            capturedConfig!.Value.TryGetProperty("defaultSrsCode", out _),
            "defaultSrsCode should be omitted from the sidecar when null.");
    }

    [TestMethod]
    public async Task RunAsyncPropagatesCallerCancellation()
    {
        var process = CreateProcess(pollIntervalMs: 50, timeoutSeconds: 60);
        var input = WriteInputXtf("roads.xtf", new byte[] { 0x01 });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(() => process.RunAsync(new[] { input }, cts.Token));
    }

    [TestMethod]
    public void ConstructorThrowsWhenInputRootIsMissing()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new IliToGeoPackageProcess(
                inputRoot: string.Empty,
                outputRoot: outputDir,
                pollIntervalMs: null,
                timeoutSeconds: null,
                import: null,
                disableValidation: null,
                createBasketCol: null,
                smart2Inheritance: null,
                schemaImport: null,
                models: null,
                defaultSrsCode: null,
                pipelineFileManager: fileManager,
                logger: loggerMock.Object));
    }

    [TestMethod]
    public void ConstructorThrowsWhenOutputRootIsMissing()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new IliToGeoPackageProcess(
                inputRoot: inputDir,
                outputRoot: string.Empty,
                pollIntervalMs: null,
                timeoutSeconds: null,
                import: null,
                disableValidation: null,
                createBasketCol: null,
                smart2Inheritance: null,
                schemaImport: null,
                models: null,
                defaultSrsCode: null,
                pipelineFileManager: fileManager,
                logger: loggerMock.Object));
    }

    [TestMethod]
    public void ConstructorCreatesInputAndOutputDirectoriesIfMissing()
    {
        Assert.IsFalse(Directory.Exists(inputDir));
        Assert.IsFalse(Directory.Exists(outputDir));

        CreateProcess();

        Assert.IsTrue(Directory.Exists(inputDir));
        Assert.IsTrue(Directory.Exists(outputDir));
    }

    /* -------------------- Helpers -------------------- */

    private IliToGeoPackageProcess CreateProcess(
        int? pollIntervalMs = 25,
        int? timeoutSeconds = 10,
        bool? import = null,
        bool? disableValidation = null,
        bool? createBasketCol = null,
        bool? smart2Inheritance = null,
        bool? schemaImport = null,
        HashSet<string>? models = null,
        int? defaultSrsCode = null)
    {
        return new IliToGeoPackageProcess(
            inputRoot: inputDir,
            outputRoot: outputDir,
            pollIntervalMs: pollIntervalMs,
            timeoutSeconds: timeoutSeconds,
            import: import,
            disableValidation: disableValidation,
            createBasketCol: createBasketCol,
            smart2Inheritance: smart2Inheritance,
            schemaImport: schemaImport,
            models: models,
            defaultSrsCode: defaultSrsCode,
            pipelineFileManager: fileManager,
            logger: loggerMock.Object);
    }

    private PipelineFile WriteInputXtf(string originalName, byte[] content)
    {
        // The process reads the file via IPipelineFile.OpenReadFileStream, so we just need a
        // real file on disk somewhere — the pipeline working directory is fine.
        Directory.CreateDirectory(pipelineWorkDir);
        var path = Path.Combine(pipelineWorkDir, originalName);
        File.WriteAllBytes(path, content);
        return new PipelineFile(path, originalName);
    }

    /// <summary>
    /// Stand-in for the real ili2gpkg-worker container. Watches the shared input directory
    /// for the <c>*.input.ready</c> sentinel, reads the per-file config sidecar, invokes a
    /// test-supplied handler, and writes the resulting <c>.gpkg</c> or <c>.error</c>
    /// payload followed by the <c>.output.ready</c> sentinel back into the shared output
    /// directory — same protocol as the production worker.
    /// </summary>
    private sealed class FakeWorker : IAsyncDisposable
    {
        private readonly CancellationTokenSource cts = new();
        private readonly Task loopTask;

        public readonly record struct Result(byte[]? Gpkg, string? Error);

        public static Result Gpkg(byte[] bytes) => new(bytes, null);

        public static Result Error(string message) => new(null, message);

        public static FakeWorker Start(string inputDir, string outputDir, Func<JsonElement, Result> handler)
        {
            return new FakeWorker(inputDir, outputDir, handler);
        }

        private FakeWorker(string inputDir, string outputDir, Func<JsonElement, Result> handler)
        {
            loopTask = Task.Run(() => RunLoopAsync(inputDir, outputDir, handler, cts.Token));
        }

        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            try { await loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on shutdown */ }
            cts.Dispose();
        }

        private static async Task RunLoopAsync(string inputDir, string outputDir, Func<JsonElement, Result> handler, CancellationToken ct)
        {
            Directory.CreateDirectory(inputDir);
            Directory.CreateDirectory(outputDir);

            while (!ct.IsCancellationRequested)
            {
                foreach (var readyPath in Directory.EnumerateFiles(inputDir, "*.input.ready"))
                {
                    var correlationId = Path.GetFileName(readyPath);
                    correlationId = correlationId.Substring(0, correlationId.Length - ".input.ready".Length);

                    var xtfPath = Path.Combine(inputDir, $"{correlationId}.xtf");
                    var configPath = Path.Combine(inputDir, $"{correlationId}.config.json");
                    if (!File.Exists(xtfPath) || !File.Exists(configPath))
                        continue;

                    await ProcessOneAsync(xtfPath, configPath, readyPath, correlationId, outputDir, handler).ConfigureAwait(false);
                }

                try { await Task.Delay(20, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static async Task ProcessOneAsync(string xtfPath, string configPath, string inputReadyPath, string correlationId, string outputDir, Func<JsonElement, Result> handler)
        {
            JsonDocument config;
            try
            {
                using var stream = File.OpenRead(configPath);
                config = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // File might still be briefly locked by the geopilot side. Skip this round;
                // the next poll will retry.
                return;
            }

            using (config)
            {
                // Pass a cloned JsonElement so the handler's captured value survives the
                // disposal of our JsonDocument.
                var result = handler(config.RootElement.Clone());
                var outputReadyPath = Path.Combine(outputDir, $"{correlationId}.output.ready");

                if (result.Gpkg != null)
                {
                    // Payload directly under final name, sentinel last — same contract as
                    // the production process.sh.
                    await File.WriteAllBytesAsync(Path.Combine(outputDir, $"{correlationId}.gpkg"), result.Gpkg).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(outputReadyPath, Array.Empty<byte>()).ConfigureAwait(false);
                }
                else if (result.Error != null)
                {
                    await File.WriteAllTextAsync(Path.Combine(outputDir, $"{correlationId}.error"), result.Error).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(outputReadyPath, Array.Empty<byte>()).ConfigureAwait(false);
                }
            }

            // Mirror the production worker's "consume inputs on success" behavior — all
            // three files (xtf, config, input.ready) are deleted together.
            TryDelete(xtfPath);
            TryDelete(configPath);
            TryDelete(inputReadyPath);
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
