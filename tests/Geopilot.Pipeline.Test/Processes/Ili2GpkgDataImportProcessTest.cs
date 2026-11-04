using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Processes.Gpkg;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class Ili2GpkgDataImportProcessTest
{
    private Mock<IIli2GpkgClient> ili2GpkgClientMock;
    private Mock<IPipelineFile> modelRepositoryMock;
    private List<ImportCall> calls;
    private bool success;
    private string? logContent;

    [TestInitialize]
    public void SetUp()
    {
        calls = [];
        success = true;
        logContent = null;
        ili2GpkgClientMock = new Mock<IIli2GpkgClient>();
        ili2GpkgClientMock
            .Setup(c => c.ImportAsync(It.IsAny<Ili2GpkgArgs>(), It.IsAny<IPipelineFile?>(), It.IsAny<IPipelineFile>(), It.IsAny<IReadOnlyList<IPipelineFile>>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IReadOnlyList<IPipelineFile>?>(), It.IsAny<CancellationToken>()))
            .Callback<Ili2GpkgArgs, IPipelineFile?, IPipelineFile, IReadOnlyList<IPipelineFile>, IPipelineFile, IPipelineFile, IPipelineFile, IReadOnlyList<IPipelineFile>?, CancellationToken>((args, inputFile, outputFile, transferFiles, errorLogFile, xtfLogFile, modelRepository, _, _) =>
            {
                calls.Add(new ImportCall(args, inputFile, outputFile, transferFiles, errorLogFile, xtfLogFile, modelRepository));
                if (logContent != null)
                {
                    using var writer = new StreamWriter(errorLogFile.OpenWriteFileStream());
                    writer.Write(logContent);
                }
            })
            .ReturnsAsync(() => new Ili2GpkgResult(success));
        modelRepositoryMock = new Mock<IPipelineFile>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulDataImport()
    {
        var process = CreateProcess();

        var result = await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.HasCount(1, result.DbFile);
        Assert.AreSame(calls.Single().OutputFile, result.DbFile[0], "The file the tool wrote to has to be the result.");
        Assert.AreEqual("data.gpkg", result.DbFile[0].OriginalFileName);
        Assert.AreEqual("import.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("import-log.xtf", result.XtfLog.OriginalFileName);

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Der Daten-Import war erfolgreich." },
            { "fr", "L'importation des données s'est déroulée avec succès." },
            { "it", "L'importazione dei dati è andata a buon fine." },
            { "en", "The data import was successful." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    public async Task ReportsFailedDataImport()
    {
        var process = CreateProcess(success: false);

        var result = await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.HasCount(0, result.DbFile, "A failed import must not hand on a half written database.");

        // The failed import is a result, not an error: the logs still have to reach the caller.
        Assert.AreEqual("import.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("import-log.xtf", result.XtfLog.OriginalFileName);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
        Assert.Contains("Fehler", result.StatusMessage["de"]);
    }

    [TestMethod]
    [DataRow(false, IlitoolsHelper.Ili2gpkgMetaConfigNotFoundMarker, "ilidata:metaconfig", DisplayName = "Unresolved meta config")]
    [DataRow(true, IlitoolsHelper.CheckNotEvaluatedMarker, "geow-interlis-functions", DisplayName = "Check not evaluated")]
    public async Task ThrowsWhenTheLogShowsAnUndefinedOutcome(bool success, string logMarker, string expectedInMessage)
    {
        logContent = $"Warning: {logMarker}\n";
        var process = CreateProcess(metaConfig: "metaconfig", pluginIds: "geow-interlis-functions", success: success);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None));

        Assert.Contains(expectedInMessage, exception.Message, "The message should name what was configured.");
    }

    [TestMethod]
    public async Task ThrowsNamingTheToolAndSettingWhenTheMetaConfigCannotBeResolved()
    {
        logContent = $"Error: {IlitoolsHelper.Ili2gpkgMetaConfigNotFoundMarker}\n";
        var process = CreateProcess(metaConfig: "metaconfig", success: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None));

        Assert.Contains("so ili2gpkg did not process", exception.Message, "The message should name the tool that ran.");
        Assert.Contains("Check the metaConfig and modelDirs", exception.Message, "The message should name the setting of this process, not the one of the XTF validator.");
    }

    [TestMethod]
    public async Task PassesLogFilesToClient()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.IsNotNull(calls.Single().LogFile);
        Assert.IsNotNull(calls.Single().XtfLogFile);
    }

    [TestMethod]
    public async Task PassesModelRepositoryToClient()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.AreSame(modelRepositoryMock.Object, calls.Single().ModelRepository);
    }

    [TestMethod]
    public async Task ImportsTheTransferFilesIntoTheGivenDatabase()
    {
        var process = CreateProcess();
        var dbFile = new PipelineFile("TestData/Ilitools/schema.gpkg", "schema.gpkg");
        IPipelineFile[] transferFiles = [CreateTransferFile("first.xtf"), CreateTransferFile("second.xtf")];

        await process.RunAsync(transferFiles, dbFile, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.AreSame(dbFile, calls.Single().InputFile, "The given database has to reach the client unchanged.");
        CollectionAssert.AreEqual(transferFiles, calls.Single().TransferFiles.ToArray(), "The wired transfer files have to reach the client unchanged and in order.");
    }

    [TestMethod]
    public async Task ThrowsWhenNoTransferFilesAreProvided()
    {
        var process = CreateProcess();

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync([], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None));

        Assert.HasCount(0, calls);
    }

    [TestMethod]
    [DataRow("first.gml", "second.gml", DisplayName = "Unsupported format")]
    [DataRow("first.xtf", "second.itf", DisplayName = "Mixed formats")]
    public async Task ThrowsWhenTheTransferFormatIsNotSupported(string firstFileName, string secondFileName)
    {
        var process = CreateProcess();
        IPipelineFile[] transferFiles = [CreateTransferFile(firstFileName), CreateTransferFile(secondFileName)];

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(transferFiles, dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None));

        Assert.HasCount(0, calls);
    }

    [TestMethod]
    [DataRow("transfer.xtf", Ili2GpkgTransferFormat.Xtf)]
    [DataRow("transfer.xml", Ili2GpkgTransferFormat.Xtf)]
    [DataRow("transfer.itf", Ili2GpkgTransferFormat.Itf)]
    public async Task PassesTheTransferFormat(string fileName, Ili2GpkgTransferFormat expectedFormat)
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile(fileName)], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.AreEqual(expectedFormat, calls.Single().Args.TransferFormat);
    }

    [TestMethod]
    public async Task PassesTheSelectionOfEachRun()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: ["ModelA", "ModelB"], defaultSrsCode: 2056, dataset: "dataset1", CancellationToken.None);

        var args = calls.Last().Args;
        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, args.Models?.ToArray());
        Assert.AreEqual(2056, args.DefaultSrsCode);
        Assert.AreEqual("dataset1", args.Dataset);

        // The selection belongs to one run: a later run without one must not inherit it.
        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        Assert.AreEqual(new Ili2GpkgArgs { TransferFormat = Ili2GpkgTransferFormat.Xtf }, calls.Last().Args);
    }

    [TestMethod]
    public async Task PassesOnlyTheModelNamesOfABlankOrEmptyEntryList()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: [" ModelA ; ;ModelB;"], defaultSrsCode: null, dataset: null, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, calls.Last().Args.Models?.ToArray());

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: [" "], defaultSrsCode: null, dataset: null, CancellationToken.None);
        Assert.IsNull(calls.Last().Args.Models);
    }

    [TestMethod]
    public async Task PassesTheConfiguredStringConfigValues()
    {
        var process = CreateProcess(metaConfig: "ilidata:metaconfig", modelDirs: "https://models.example.com/;%XTF_DIR/models", toolVersion: "5.2.0", pluginIds: "geow-interlis-functions;ngk-so");

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

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

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

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

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

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

        await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);

        // Only the format is always set, because it comes from the transfer files and not from the configuration.
        Assert.AreEqual(new Ili2GpkgArgs { TransferFormat = Ili2GpkgTransferFormat.Xtf }, calls.Single().Args, "Missing configuration must leave every tool argument at its default.");
    }

    [TestMethod]
    public async Task SetsTheToolArgumentNamedAfterEachConstructorParameter()
    {
        var dependencies = new Dictionary<string, object?>
        {
            ["modelRepository"] = null,
            ["ili2GpkgClient"] = ili2GpkgClientMock.Object,
            ["pipelineFileManager"] = CreatePipelineFileManager(),
            ["logger"] = Mock.Of<ILogger<Ili2GpkgDataImportProcessTest>>(),
        };

        await Ili2GpkgArgsAssert.SetsTheArgumentNamedAfterEachConstructorParameter<Ili2GpkgDataImportProcess>(dependencies, async process =>
        {
            calls.Clear();
            await process.RunAsync([CreateTransferFile()], dbFile: null, models: null, defaultSrsCode: null, dataset: null, CancellationToken.None);
            return calls.Single().Args;
        });
    }

    private static PipelineFile CreateTransferFile(string originalFileName = "transfer.xtf")
    {
        return new PipelineFile("TestData/Ilitools/transfer.xtf", originalFileName);
    }

    private static PipelineFileManager CreatePipelineFileManager()
    {
        return new PipelineFileManager(Path.GetTempPath(), "Ili2GpkgDataImportProcess");
    }

    private Ili2GpkgDataImportProcess CreateProcess(string? metaConfig = null, string? modelDirs = null, string? toolVersion = null, string? pluginIds = null, bool success = true)
    {
        this.success = success;
        return new Ili2GpkgDataImportProcess(metaConfig, modelDirs, toolVersion, pluginIds, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, modelRepositoryMock.Object, ili2GpkgClientMock.Object, CreatePipelineFileManager(), Mock.Of<ILogger<Ili2GpkgDataImportProcessTest>>());
    }

    private sealed record ImportCall(Ili2GpkgArgs Args, IPipelineFile? InputFile, IPipelineFile OutputFile, IReadOnlyList<IPipelineFile> TransferFiles, IPipelineFile LogFile, IPipelineFile XtfLogFile, IPipelineFile ModelRepository);
}
