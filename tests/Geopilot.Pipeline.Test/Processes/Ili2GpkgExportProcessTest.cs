using Geopilot.Pipeline.Processes.Gpkg;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class Ili2GpkgExportProcessTest
{
    private Mock<IIli2GpkgClient> ili2GpkgClientMock;
    private Mock<IPipelineFile> modelRepositoryMock;
    private List<ExportCall> calls;
    private bool success;

    [TestInitialize]
    public void SetUp()
    {
        calls = [];
        success = true;
        ili2GpkgClientMock = new Mock<IIli2GpkgClient>();
        ili2GpkgClientMock
            .Setup(c => c.ExportAsync(It.IsAny<Ili2GpkgArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile?>(), It.IsAny<IReadOnlyList<IPipelineFile>?>(), It.IsAny<CancellationToken>()))
            .Callback<Ili2GpkgArgs, IPipelineFile, IPipelineFile, IPipelineFile, IPipelineFile, IPipelineFile?, IReadOnlyList<IPipelineFile>?, CancellationToken>((args, gpkgFile, transferFile, errorLogFile, xtfLogFile, modelRepository, _, _) =>
                calls.Add(new ExportCall(args, gpkgFile, transferFile, errorLogFile, xtfLogFile, modelRepository)))
            .ReturnsAsync(() => new Ili2GpkgResult(success));
        modelRepositoryMock = new Mock<IPipelineFile>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulDataExport()
    {
        var process = CreateProcess();

        var result = await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.HasCount(1, result.TransferFile);
        Assert.AreSame(calls.Single().TransferFile, result.TransferFile[0], "The file the tool wrote to has to be the result.");
        Assert.AreEqual("data.xtf", result.TransferFile[0].OriginalFileName);
        Assert.AreEqual("export.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("export-log.xtf", result.XtfLog.OriginalFileName);

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Der Daten-Export war erfolgreich." },
            { "fr", "L'exportation des données s'est déroulée avec succès." },
            { "it", "L'esportazione dei dati è andata a buon fine." },
            { "en", "The data export was successful." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    public async Task ReportsFailedDataExport()
    {
        var process = CreateProcess(success: false);

        var result = await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.HasCount(0, result.TransferFile, "A failed export must not hand on a half written transfer file.");

        // The failed export is a result, not an error: the logs still have to reach the caller.
        Assert.AreEqual("export.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("export-log.xtf", result.XtfLog.OriginalFileName);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
        Assert.Contains("Fehler", result.StatusMessage["de"]);
    }

    [TestMethod]
    public async Task PassesLogFilesToClient()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        Assert.IsNotNull(calls.Single().LogFile);
        Assert.IsNotNull(calls.Single().XtfLogFile);
    }

    [TestMethod]
    public async Task PassesModelRepositoryToClient()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        Assert.AreSame(modelRepositoryMock.Object, calls.Single().ModelRepository);
    }

    [TestMethod]
    public async Task ExportsTheGivenDatabase()
    {
        var process = CreateProcess();
        var dbFile = CreateDbFile();

        await process.RunAsync(dbFile, models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        Assert.AreSame(dbFile, calls.Single().GpkgFile, "The given database has to reach the client unchanged.");
    }

    [TestMethod]
    [DataRow(null, null, "data.xtf")]
    [DataRow("xtf", Ili2GpkgTransferFormat.Xtf, "data.xtf")]
    [DataRow("itf", Ili2GpkgTransferFormat.Itf, "data.itf")]
    [DataRow("gml", Ili2GpkgTransferFormat.Gml, "data.gml")]
    [DataRow("gml2", Ili2GpkgTransferFormat.Gml20, "data.gml")]
    public async Task WritesTheTransferFileInTheRequestedFormat(string? transferFormat, Ili2GpkgTransferFormat? expectedFormat, string expectedFileName)
    {
        var process = CreateProcess();

        var result = await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat, CancellationToken.None);

        Assert.AreEqual(expectedFormat, calls.Single().Args.TransferFormat);
        Assert.AreEqual(expectedFileName, result.TransferFile.Single().OriginalFileName);
    }

    [TestMethod]
    public async Task ThrowsWhenTheTransferFormatIsNotSupported()
    {
        var process = CreateProcess();

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: "gpkg", CancellationToken.None));

        Assert.HasCount(0, calls);
    }

    [TestMethod]
    public async Task PassesTheSelectionOfEachRun()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: "ModelA;ModelB", topics: "ModelA.TopicA;ModelB.TopicB", baskets: "b1;b2", dataset: "dataset1", exportModels: "BaseModelA;BaseModelB", transferFormat: null, CancellationToken.None);

        var args = calls.Last().Args;
        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, args.Models?.ToArray());
        CollectionAssert.AreEqual(new[] { "ModelA.TopicA", "ModelB.TopicB" }, args.Topics?.ToArray());
        CollectionAssert.AreEqual(new[] { "b1", "b2" }, args.Baskets?.ToArray());
        Assert.AreEqual("dataset1", args.Dataset);
        CollectionAssert.AreEqual(new[] { "BaseModelA", "BaseModelB" }, args.ExportModels?.ToArray());

        // The selection belongs to one run: a later run without one must export everything again.
        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        Assert.AreEqual(new Ili2GpkgArgs(), calls.Last().Args);
    }

    [TestMethod]
    public async Task PassesTheConfiguredStringConfigValues()
    {
        var process = CreateProcess(metaConfig: "ilidata:metaconfig", modelDirs: "https://models.example.com/;%XTF_DIR/models", toolVersion: "5.2.0", pluginIds: "geow-interlis-functions;ngk-so");

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        // The order of the repositories decides which model wins, so it must survive unchanged.
        var args = calls.Single().Args;
        Assert.AreEqual("ilidata:metaconfig", args.MetaConfig);
        CollectionAssert.AreEqual(new[] { "https://models.example.com/", "%XTF_DIR/models" }, args.ModelDirs?.ToArray());
        Assert.AreEqual("5.2.0", args.ToolVersion);
        CollectionAssert.AreEqual(new[] { "geow-interlis-functions", "ngk-so" }, args.PluginIds?.ToArray());
    }

    [TestMethod]
    public async Task TrimsTheConfiguredStringConfigValues()
    {
        // Hand written configuration, so blanks around the separator and a trailing one are expected.
        var process = CreateProcess(metaConfig: " metaconfig ", modelDirs: " https://models.example.com/ ; %XTF_DIR/models ; ", toolVersion: " 5.2.0 ", pluginIds: " geow-interlis-functions ; ngk-so ; ");

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        var args = calls.Single().Args;
        CollectionAssert.AreEqual(new[] { "https://models.example.com/", "%XTF_DIR/models" }, args.ModelDirs?.ToArray());
        Assert.AreEqual("5.2.0", args.ToolVersion);
        CollectionAssert.AreEqual(new[] { "geow-interlis-functions", "ngk-so" }, args.PluginIds?.ToArray());

        // The meta config is an ilidata reference, so it automatically gets the "ilidata:" prefix.
        Assert.AreEqual("ilidata:metaconfig", args.MetaConfig);
    }

    [TestMethod]
    public async Task PassesNoOptionsWhenOnlyBlanksAreConfigured()
    {
        // Passed on, a blank would reach the wrapper as an empty repository, plugin or version name and be rejected.
        var process = CreateProcess(metaConfig: "   ", modelDirs: " ; ", toolVersion: "   ", pluginIds: " ; ");

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        var args = calls.Single().Args;
        Assert.IsNull(args.MetaConfig);
        Assert.IsNull(args.ModelDirs);
        Assert.IsNull(args.ToolVersion);
        Assert.IsNull(args.PluginIds);
    }

    [TestMethod]
    public async Task PassesNoOptionsWhenNoneAreConfigured()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);

        // Without a format the tool writes its own default, xtf.
        Assert.AreEqual(new Ili2GpkgArgs(), calls.Single().Args, "Missing configuration must leave every tool argument at its default.");
    }

    [TestMethod]
    public async Task SetsTheToolArgumentNamedAfterEachConstructorParameter()
    {
        var dependencies = new Dictionary<string, object?>
        {
            ["modelRepository"] = null,
            ["ili2GpkgClient"] = ili2GpkgClientMock.Object,
            ["pipelineFileManager"] = CreatePipelineFileManager(),
            ["logger"] = Mock.Of<ILogger<Ili2GpkgExportProcessTest>>(),
        };

        await Ili2GpkgArgsAssert.SetsTheArgumentNamedAfterEachConstructorParameter<Ili2GpkgExportProcess>(dependencies, async process =>
        {
            calls.Clear();
            await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, exportModels: null, transferFormat: null, CancellationToken.None);
            return calls.Single().Args;
        });
    }

    private static PipelineFile CreateDbFile()
    {
        return new PipelineFile("TestData/Ilitools/data.gpkg", "data.gpkg");
    }

    private static PipelineFileManager CreatePipelineFileManager()
    {
        return new PipelineFileManager(Path.GetTempPath(), "Ili2GpkgExportProcess");
    }

    private Ili2GpkgExportProcess CreateProcess(string? metaConfig = null, string? modelDirs = null, string? toolVersion = null, string? pluginIds = null, bool success = true)
    {
        this.success = success;
        return new Ili2GpkgExportProcess(metaConfig, modelDirs, toolVersion, pluginIds, null, modelRepositoryMock.Object, ili2GpkgClientMock.Object, CreatePipelineFileManager(), Mock.Of<ILogger<Ili2GpkgExportProcessTest>>());
    }

    private sealed record ExportCall(Ili2GpkgArgs Args, IPipelineFile GpkgFile, IPipelineFile TransferFile, IPipelineFile LogFile, IPipelineFile XtfLogFile, IPipelineFile ModelRepository);
}
