using Geopilot.Pipeline.Processes.Gpkg;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class Ili2GpkgUpdateProcessTest
{
    private Mock<IIli2GpkgClient> ili2GpkgClientMock;
    private Mock<IPipelineFile> modelRepositoryMock;
    private List<UpdateCall> calls;
    private bool success;

    [TestInitialize]
    public void SetUp()
    {
        calls = [];
        success = true;
        ili2GpkgClientMock = new Mock<IIli2GpkgClient>();
        ili2GpkgClientMock
            .Setup(c => c.UpdateAsync(It.IsAny<Ili2GpkgArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IReadOnlyList<IPipelineFile>>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IReadOnlyList<IPipelineFile>?>(), It.IsAny<CancellationToken>()))
            .Callback<Ili2GpkgArgs, IPipelineFile, IPipelineFile, IReadOnlyList<IPipelineFile>, IPipelineFile, IPipelineFile, IPipelineFile, IReadOnlyList<IPipelineFile>?, CancellationToken>((args, inputFile, outputFile, transferFiles, errorLogFile, xtfLogFile, modelRepository, _, _) =>
                calls.Add(new UpdateCall(args, inputFile, outputFile, transferFiles, errorLogFile, xtfLogFile, modelRepository)))
            .ReturnsAsync(() => new Ili2GpkgResult(success));
        modelRepositoryMock = new Mock<IPipelineFile>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulDataUpdate()
    {
        var process = CreateProcess();

        var result = await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.HasCount(1, result.DbFile);
        Assert.AreSame(calls.Single().OutputFile, result.DbFile[0], "The file the tool wrote to has to be the result.");
        Assert.AreEqual("data.gpkg", result.DbFile[0].OriginalFileName);
        Assert.AreEqual("update.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("update-log.xtf", result.XtfLog.OriginalFileName);

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Die Daten-Aktualisierung war erfolgreich." },
            { "fr", "La mise à jour des données s'est déroulée avec succès." },
            { "it", "L'aggiornamento dei dati è andato a buon fine." },
            { "en", "The data update was successful." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    public async Task ReportsFailedDataUpdate()
    {
        var process = CreateProcess(success: false);

        var result = await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.HasCount(0, result.DbFile, "A failed update must not hand on a half written database.");

        // The failed update is a result, not an error: the logs still have to reach the caller.
        Assert.AreEqual("update.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("update-log.xtf", result.XtfLog.OriginalFileName);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
        Assert.Contains("Fehler", result.StatusMessage["de"]);
    }

    [TestMethod]
    public async Task PassesLogFilesToClient()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsNotNull(calls.Single().LogFile);
        Assert.IsNotNull(calls.Single().XtfLogFile);
    }

    [TestMethod]
    public async Task PassesModelRepositoryToClient()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.AreSame(modelRepositoryMock.Object, calls.Single().ModelRepository);
    }

    [TestMethod]
    public async Task UpdatesTheGivenDatabaseWithTheTransferFiles()
    {
        var process = CreateProcess();
        var dbFile = CreateDbFile();
        IPipelineFile[] transferFiles = [CreateTransferFile("first.xtf"), CreateTransferFile("second.xtf")];

        await process.RunAsync(transferFiles, dbFile, models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.AreSame(dbFile, calls.Single().InputFile, "The given database has to reach the client unchanged.");
        Assert.AreNotSame(dbFile, calls.Single().OutputFile, "The update has to write a new database instead of changing the given one.");
        CollectionAssert.AreEqual(transferFiles, calls.Single().TransferFiles.ToArray(), "The wired transfer files have to reach the client unchanged and in order.");
    }

    [TestMethod]
    public async Task ThrowsWhenNoTransferFilesAreProvided()
    {
        var process = CreateProcess();

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync([], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None));

        Assert.HasCount(0, calls);
    }

    [TestMethod]
    [DataRow("first.gml", "second.gml", DisplayName = "Unsupported format")]
    [DataRow("first.xtf", "second.itf", DisplayName = "Mixed formats")]
    public async Task ThrowsWhenTheTransferFormatIsNotSupported(string firstFileName, string secondFileName)
    {
        var process = CreateProcess();
        IPipelineFile[] transferFiles = [CreateTransferFile(firstFileName), CreateTransferFile(secondFileName)];

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(transferFiles, CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None));

        Assert.HasCount(0, calls);
    }

    [TestMethod]
    [DataRow("transfer.xtf", Ili2GpkgTransferFormat.Xtf)]
    [DataRow("transfer.xml", Ili2GpkgTransferFormat.Xtf)]
    [DataRow("transfer.itf", Ili2GpkgTransferFormat.Itf)]
    public async Task PassesTheTransferFormat(string fileName, Ili2GpkgTransferFormat expectedFormat)
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile(fileName)], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.AreEqual(expectedFormat, calls.Single().Args.TransferFormat);
    }

    [TestMethod]
    public async Task PassesTheSelectionOfEachRun()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: "ModelA;ModelB", dataset: "dataset1", defaultSrsCode: 2056, CancellationToken.None);

        var args = calls.Last().Args;
        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, args.Models?.ToArray());
        Assert.AreEqual("dataset1", args.Dataset);
        Assert.AreEqual(2056, args.DefaultSrsCode);

        // The selection belongs to one run: a later run without one must not inherit it.
        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

        Assert.AreEqual(new Ili2GpkgArgs { TransferFormat = Ili2GpkgTransferFormat.Xtf }, calls.Last().Args);
    }

    [TestMethod]
    public async Task PassesTheConfiguredStringConfigValues()
    {
        var process = CreateProcess(metaConfig: "ilidata:metaconfig", modelDirs: "https://models.example.com/;%XTF_DIR/models", toolVersion: "5.2.0", pluginIds: "geow-interlis-functions;ngk-so");

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

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

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

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

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

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

        await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);

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
            ["logger"] = Mock.Of<ILogger<Ili2GpkgUpdateProcessTest>>(),
        };

        await Ili2GpkgArgsAssert.SetsTheArgumentNamedAfterEachConstructorParameter<Ili2GpkgUpdateProcess>(dependencies, async process =>
        {
            calls.Clear();
            await process.RunAsync([CreateTransferFile()], CreateDbFile(), models: null, dataset: null, defaultSrsCode: null, CancellationToken.None);
            return calls.Single().Args;
        });
    }

    private static PipelineFile CreateTransferFile(string originalFileName = "transfer.xtf")
    {
        return new PipelineFile("TestData/Ilitools/transfer.xtf", originalFileName);
    }

    private static PipelineFile CreateDbFile()
    {
        return new PipelineFile("TestData/Ilitools/data.gpkg", "data.gpkg");
    }

    private static PipelineFileManager CreatePipelineFileManager()
    {
        return new PipelineFileManager(Path.GetTempPath(), "Ili2GpkgUpdateProcess");
    }

    private Ili2GpkgUpdateProcess CreateProcess(string? metaConfig = null, string? modelDirs = null, string? toolVersion = null, string? pluginIds = null, bool success = true)
    {
        this.success = success;
        return new Ili2GpkgUpdateProcess(metaConfig, modelDirs, toolVersion, pluginIds, null, null, null, null, modelRepositoryMock.Object, ili2GpkgClientMock.Object, CreatePipelineFileManager(), Mock.Of<ILogger<Ili2GpkgUpdateProcessTest>>());
    }

    private sealed record UpdateCall(Ili2GpkgArgs Args, IPipelineFile InputFile, IPipelineFile OutputFile, IReadOnlyList<IPipelineFile> TransferFiles, IPipelineFile LogFile, IPipelineFile XtfLogFile, IPipelineFile? ModelRepository);
}
