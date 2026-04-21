using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.IliToGeoPackage;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process.Container;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class IliToGeoPackageProcessTest
{
    private Mock<IContainerRunner> runnerMock = null!;
    private Mock<ILogger> loggerMock = null!;
    private string tempDir = null!;
    private PipelineFileManager fileManager = null!;

    [TestInitialize]
    public void SetUp()
    {
        runnerMock = new Mock<IContainerRunner>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger>();
        tempDir = Path.Combine(Path.GetTempPath(), "ili2gpkg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        fileManager = new PipelineFileManager(tempDir, "step1");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [TestMethod]
    public async Task RunBuildsMinimalIli2GpkgCommand()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53, 0x51, 0x4c });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        Assert.AreEqual(IliToGeoPackageProcess.Image, capturedSpec!.Image);
        CollectionAssert.AreEqual(
            new[] { "--dbfile", "/data/out/roads.gpkg", "--import", "/data/in/roads.xtf" },
            capturedSpec.Command.ToArray());
        Assert.HasCount(2, capturedSpec.VolumeMounts);

        var gpkgFiles = (IPipelineFile[])result["gpkg_files"]!;
        Assert.HasCount(1, gpkgFiles);
        Assert.AreEqual("roads", gpkgFiles[0].OriginalFileNameWithoutExtension);
        Assert.AreEqual("gpkg", gpkgFiles[0].FileExtension);

        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        Assert.HasCount(4, statusMessage);
        Assert.AreEqual("1 GeoPackage created", statusMessage["en"]);
        Assert.AreEqual("1 GeoPackage erstellt", statusMessage["de"]);
        Assert.AreEqual("1 GeoPackage créé", statusMessage["fr"]);
        Assert.AreEqual("1 GeoPackage creato", statusMessage["it"]);
    }

    [TestMethod]
    public async Task RunReturnsPluralStatusMessageForMultipleFiles()
    {
        var inputPath1 = Path.Combine(tempDir, "roads.xtf");
        var inputPath2 = Path.Combine(tempDir, "buildings.xtf");
        await File.WriteAllTextAsync(inputPath1, "<xtf/>");
        await File.WriteAllTextAsync(inputPath2, "<xtf/>");
        var files = new IPipelineFile[]
        {
            new PipelineFile(inputPath1, "roads.xtf"),
            new PipelineFile(inputPath2, "buildings.xtf"),
        };

        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                var dbFileArgIndex = spec.Command.ToList().IndexOf("--dbfile") + 1;
                var gpkgFileName = Path.GetFileName(spec.Command[dbFileArgIndex]);
                File.WriteAllBytes(Path.Combine(outMount.HostPath, gpkgFileName), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var result = await process.RunAsync(files, CancellationToken.None);

        var gpkgFiles = (IPipelineFile[])result["gpkg_files"]!;
        Assert.HasCount(2, gpkgFiles);

        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        Assert.AreEqual("2 GeoPackages created", statusMessage["en"]);
        Assert.AreEqual("2 GeoPackages erstellt", statusMessage["de"]);
        Assert.AreEqual("2 GeoPackages créés", statusMessage["fr"]);
        Assert.AreEqual("2 GeoPackages creati", statusMessage["it"]);
    }

    [TestMethod]
    public async Task RunReturnsStatusMessageWithWarningsWhenExitCodeNonZero()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(1, string.Empty, "no CRS for attribute ...", TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var result = await process.RunAsync(files, CancellationToken.None);

        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        Assert.AreEqual("1 GeoPackage created with warnings", statusMessage["en"]);
        Assert.AreEqual("1 GeoPackage mit Warnungen erstellt", statusMessage["de"]);
        Assert.AreEqual("1 GeoPackage créé avec des avertissements", statusMessage["fr"]);
        Assert.AreEqual("1 GeoPackage creato con avvisi", statusMessage["it"]);
    }

    [TestMethod]
    public async Task RunReturnsMixedStatusMessageWhenSomeFilesHaveWarnings()
    {
        var inputPath1 = Path.Combine(tempDir, "roads.xtf");
        var inputPath2 = Path.Combine(tempDir, "buildings.xtf");
        var inputPath3 = Path.Combine(tempDir, "parcels.xtf");
        await File.WriteAllTextAsync(inputPath1, "<xtf/>");
        await File.WriteAllTextAsync(inputPath2, "<xtf/>");
        await File.WriteAllTextAsync(inputPath3, "<xtf/>");
        var files = new IPipelineFile[]
        {
            new PipelineFile(inputPath1, "roads.xtf"),
            new PipelineFile(inputPath2, "buildings.xtf"),
            new PipelineFile(inputPath3, "parcels.xtf"),
        };

        // Runner returns exit 1 for buildings.xtf (simulating warnings) but exit 0 for the others.
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Returns<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                var dbFileArgIndex = spec.Command.ToList().IndexOf("--dbfile") + 1;
                var gpkgFileName = Path.GetFileName(spec.Command[dbFileArgIndex]);
                File.WriteAllBytes(Path.Combine(outMount.HostPath, gpkgFileName), new byte[] { 0x53 });

                var exitCode = gpkgFileName == "buildings.gpkg" ? 1L : 0L;
                return Task.FromResult(new ContainerRunResult(exitCode, string.Empty, string.Empty, TimeSpan.Zero));
            });

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var result = await process.RunAsync(files, CancellationToken.None);

        var gpkgFiles = (IPipelineFile[])result["gpkg_files"]!;
        Assert.HasCount(3, gpkgFiles);

        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        Assert.AreEqual("3 GeoPackages created, 1 with warnings", statusMessage["en"]);
        Assert.AreEqual("3 GeoPackages erstellt, 1 mit Warnungen", statusMessage["de"]);
        Assert.AreEqual("3 GeoPackages créés, 1 avec des avertissements", statusMessage["fr"]);
        Assert.AreEqual("3 GeoPackages creati, 1 con avvisi", statusMessage["it"]);
    }

    [TestMethod]
    public async Task RunIncludesDisableValidationFlagWhenConfigured()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: true,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        CollectionAssert.Contains(capturedSpec!.Command.ToArray(), "--disableValidation");
    }

    [TestMethod]
    public async Task RunOmitsDisableValidationFlagByDefault()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: false,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        CollectionAssert.DoesNotContain(capturedSpec!.Command.ToArray(), "--disableValidation");
    }

    [TestMethod]
    [DataRow(true, "--import", DisplayName = "import=true adds --import")]
    [DataRow(false, "--import", DisplayName = "import=false omits --import")]
    public async Task RunEmitsImportFlagWhenConfigured(bool enabled, string expectedFlag)
    {
        var capturedSpec = await CaptureSpecWithConfiguredFlags(import: enabled);
        if (enabled)
            CollectionAssert.Contains(capturedSpec!.Command.ToArray(), expectedFlag);
        else
            CollectionAssert.DoesNotContain(capturedSpec!.Command.ToArray(), expectedFlag);
    }

    [TestMethod]
    [DataRow(true, "--createBasketCol", DisplayName = "createBasketCol=true adds --createBasketCol")]
    [DataRow(false, "--createBasketCol", DisplayName = "createBasketCol=false omits --createBasketCol")]
    public async Task RunEmitsCreateBasketColFlagWhenConfigured(bool enabled, string expectedFlag)
    {
        var capturedSpec = await CaptureSpecWithConfiguredFlags(createBasketCol: enabled);
        if (enabled)
            CollectionAssert.Contains(capturedSpec!.Command.ToArray(), expectedFlag);
        else
            CollectionAssert.DoesNotContain(capturedSpec!.Command.ToArray(), expectedFlag);
    }

    [TestMethod]
    [DataRow(true, "--smart2Inheritance", DisplayName = "smart2Inheritance=true adds --smart2Inheritance")]
    [DataRow(false, "--smart2Inheritance", DisplayName = "smart2Inheritance=false omits --smart2Inheritance")]
    public async Task RunEmitsSmart2InheritanceFlagWhenConfigured(bool enabled, string expectedFlag)
    {
        var capturedSpec = await CaptureSpecWithConfiguredFlags(smart2Inheritance: enabled);
        if (enabled)
            CollectionAssert.Contains(capturedSpec!.Command.ToArray(), expectedFlag);
        else
            CollectionAssert.DoesNotContain(capturedSpec!.Command.ToArray(), expectedFlag);
    }

    [TestMethod]
    [DataRow(true, "--schemaimport", DisplayName = "schemaImport=true adds --schemaimport")]
    [DataRow(false, "--schemaimport", DisplayName = "schemaImport=false omits --schemaimport")]
    public async Task RunEmitsSchemaImportFlagWhenConfigured(bool enabled, string expectedFlag)
    {
        var capturedSpec = await CaptureSpecWithConfiguredFlags(schemaImport: enabled);
        if (enabled)
            CollectionAssert.Contains(capturedSpec!.Command.ToArray(), expectedFlag);
        else
            CollectionAssert.DoesNotContain(capturedSpec!.Command.ToArray(), expectedFlag);
    }

    private async Task<ContainerRunSpec?> CaptureSpecWithConfiguredFlags(
        bool? import = null,
        bool? createBasketCol = null,
        bool? smart2Inheritance = null,
        bool? schemaImport = null)
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: import,
            disableValidation: null,
            createBasketCol: createBasketCol,
            smart2Inheritance: smart2Inheritance,
            schemaImport: schemaImport,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);
        return capturedSpec;
    }

    [TestMethod]
    public async Task RunIncludesModelsArgWhenConfigured()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: new HashSet<string> { "RoadsExdm2ien", "Units" },
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        var command = capturedSpec!.Command.ToArray();
        CollectionAssert.Contains(command, "--models");
        CollectionAssert.Contains(command, "RoadsExdm2ien;Units");
    }

    [TestMethod]
    public async Task RunBindMountsAndPassesModelDirWhenConfigured()
    {
        var modelDir = Path.Combine(tempDir, "models");
        Directory.CreateDirectory(modelDir);
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: modelDir,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        Assert.HasCount(3, capturedSpec!.VolumeMounts);
        Assert.IsTrue(capturedSpec.VolumeMounts.Any(m => m.HostPath == modelDir && m.ContainerPath == "/data/models"));
        var command = capturedSpec.Command.ToArray();
        CollectionAssert.Contains(command, "--modeldir");
        CollectionAssert.Contains(command, "/data/models");
    }

    [TestMethod]
    public async Task RunBindMountsIliCacheDirWhenConfigured()
    {
        var iliCacheDir = Path.Combine(tempDir, "ili-cache");
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: iliCacheDir,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        // Constructor must have created the cache dir on the host (so Docker's bind mount finds it).
        Assert.IsTrue(Directory.Exists(iliCacheDir), "Cache directory should be auto-created by the process constructor.");

        Assert.IsNotNull(capturedSpec);
        Assert.HasCount(3, capturedSpec!.VolumeMounts);
        Assert.IsTrue(capturedSpec.VolumeMounts.Any(m => m.HostPath == iliCacheDir && m.ContainerPath == "/root/.ilicache"));
    }

    [TestMethod]
    public async Task RunIncludesDefaultSrsCodeArgWhenConfigured()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: 2056,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        var command = capturedSpec!.Command.ToArray();
        CollectionAssert.Contains(command, "--defaultSrsCode");
        CollectionAssert.Contains(command, "2056");
    }

    [TestMethod]
    public async Task RunOmitsDefaultSrsCodeArgWhenNull()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        ContainerRunSpec? capturedSpec = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                capturedSpec = spec;
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        await process.RunAsync(files, CancellationToken.None);

        Assert.IsNotNull(capturedSpec);
        CollectionAssert.DoesNotContain(capturedSpec!.Command.ToArray(), "--defaultSrsCode");
    }

    [TestMethod]
    public async Task RunTreatsNonZeroExitAsSuccessWhenOutputProduced()
    {
        // ili2gpkg returns exit code 1 even on successful imports that had warnings (e.g. "no CRS").
        // As long as the expected output file exists, the run is considered successful.
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53, 0x51, 0x4C });
            })
            .ReturnsAsync(new ContainerRunResult(1, string.Empty, "no CRS for attribute ...", TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var result = await process.RunAsync(files, CancellationToken.None);
        var gpkgFiles = (IPipelineFile[])result["gpkg_files"]!;
        Assert.HasCount(1, gpkgFiles);
    }

    [TestMethod]
    public async Task RunThrowsWhenIli2GpkgDoesNotProduceExpectedOutput()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var files = new IPipelineFile[] { new PipelineFile(inputPath, "roads.xtf") };

        // Runner reports success but produces no output file.
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerRunResult(0, "nothing written", string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(files, CancellationToken.None));
        StringAssert.Contains(ex.Message, "did not produce");
    }

    /// <summary>
    /// Focused tests for the internal <see cref="IliToGeoPackageProcess.ConvertFileAsync"/> helper.
    /// These exercise per-file behavior that is awkward to observe through <c>RunAsync</c>.
    /// </summary>
    [TestMethod]
    public async Task ConvertFileAsyncCopiesInputBytesIntoBindMount()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        var originalBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03 };
        await File.WriteAllBytesAsync(inputPath, originalBytes);
        var inputFile = new PipelineFile(inputPath, "roads.xtf");

        byte[]? copiedBytes = null;
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                // Read the file the process copied into the /data/in bind mount.
                var inMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/in");
                copiedBytes = File.ReadAllBytes(Path.Combine(inMount.HostPath, "roads.xtf"));

                // Satisfy the output-file-must-exist contract.
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var workDir = process.PrepareWorkDirectory();
        await process.ConvertFileAsync(inputFile, workDir, CancellationToken.None);

        Assert.IsNotNull(copiedBytes);
        CollectionAssert.AreEqual(originalBytes, copiedBytes);
    }

    [TestMethod]
    public async Task ConvertFileAsyncReturnsHadWarningsTrueForNonZeroExit()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var inputFile = new PipelineFile(inputPath, "roads.xtf");

        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(1, string.Empty, "no CRS ...", TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var workDir = process.PrepareWorkDirectory();
        var result = await process.ConvertFileAsync(inputFile, workDir, CancellationToken.None);

        Assert.IsTrue(result.HadWarnings);
        Assert.IsNotNull(result.GpkgFile);
    }

    [TestMethod]
    public async Task ConvertFileAsyncReturnsHadWarningsFalseForZeroExit()
    {
        var inputPath = Path.Combine(tempDir, "roads.xtf");
        await File.WriteAllTextAsync(inputPath, "<xtf/>");
        var inputFile = new PipelineFile(inputPath, "roads.xtf");

        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                File.WriteAllBytes(Path.Combine(outMount.HostPath, "roads.gpkg"), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var workDir = process.PrepareWorkDirectory();
        var result = await process.ConvertFileAsync(inputFile, workDir, CancellationToken.None);

        Assert.IsFalse(result.HadWarnings);
    }

    [TestMethod]
    public async Task ConvertFileAsyncReusesTheSharedWorkDirectoryAcrossCalls()
    {
        var inputPath1 = Path.Combine(tempDir, "roads.xtf");
        var inputPath2 = Path.Combine(tempDir, "buildings.xtf");
        await File.WriteAllTextAsync(inputPath1, "<xtf/>");
        await File.WriteAllTextAsync(inputPath2, "<xtf/>");
        var inputFile1 = new PipelineFile(inputPath1, "roads.xtf");
        var inputFile2 = new PipelineFile(inputPath2, "buildings.xtf");

        var seenInputMounts = new List<string>();
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                var inMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/in");
                seenInputMounts.Add(inMount.HostPath);
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                var dbFileArgIndex = spec.Command.ToList().IndexOf("--dbfile") + 1;
                var gpkgFileName = Path.GetFileName(spec.Command[dbFileArgIndex]);
                File.WriteAllBytes(Path.Combine(outMount.HostPath, gpkgFileName), new byte[] { 0x53 });
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));

        var process = new IliToGeoPackageProcess(
            import: null,
            disableValidation: null,
            createBasketCol: null,
            smart2Inheritance: null,
            schemaImport: null,
            models: null,
            modelDir: null,
            defaultSrsCode: null,
            iliCacheDir: null,
            runnerMock.Object,
            fileManager,
            loggerMock.Object);

        var workDir = process.PrepareWorkDirectory();
        await process.ConvertFileAsync(inputFile1, workDir, CancellationToken.None);
        await process.ConvertFileAsync(inputFile2, workDir, CancellationToken.None);

        Assert.HasCount(2, seenInputMounts);
        Assert.AreEqual(seenInputMounts[0], seenInputMounts[1], "Expected both invocations to share the same bind-mount path.");
    }
}
