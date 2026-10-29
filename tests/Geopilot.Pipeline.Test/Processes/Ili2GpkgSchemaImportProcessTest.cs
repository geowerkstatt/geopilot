using Geopilot.Pipeline.Processes.Gpkg;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class Ili2GpkgSchemaImportProcessTest
{
    private Mock<IIli2GpkgClient> ili2GpkgClientMock;
    private Mock<IPipelineFile> modelRepositoryMock;
    private List<SchemaImportCall> calls;
    private string? failingModel;

    [TestInitialize]
    public void SetUp()
    {
        calls = [];
        failingModel = null;
        ili2GpkgClientMock = new Mock<IIli2GpkgClient>();
        ili2GpkgClientMock
            .Setup(c => c.SchemaImportAsync(It.IsAny<Ili2GpkgArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile?>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile?>(), It.IsAny<CancellationToken>()))
            .Callback<Ili2GpkgArgs, IPipelineFile, IPipelineFile, IPipelineFile?, IPipelineFile, IPipelineFile, IPipelineFile?, CancellationToken>((args, modelFile, outputFile, errorLogFile, xtfLogFile, inputFile, modelRepository, _) =>
                calls.Add(new SchemaImportCall(args, modelFile, outputFile, errorLogFile, xtfLogFile, inputFile, modelRepository)))
            .ReturnsAsync((Ili2GpkgArgs _, IPipelineFile modelFile, IPipelineFile _, IPipelineFile? _, IPipelineFile _, IPipelineFile _, IPipelineFile? _, CancellationToken _) =>
                new Ili2GpkgResult(modelFile.OriginalFileName != failingModel));
        modelRepositoryMock = new Mock<IPipelineFile>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulSchemaImport()
    {
        var process = CreateProcess();

        var result = await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.HasCount(1, result.DbFile);
        Assert.AreSame(calls.Single().OutputFile, result.DbFile[0], "The file the tool wrote to has to be the result.");
        Assert.AreEqual("schema.gpkg", result.DbFile[0].OriginalFileName);
        CollectionAssert.AreEqual(new[] { "schema-import.log" }, OriginalFileNames(result.ErrorLogs));
        CollectionAssert.AreEqual(new[] { "schema-import-log.xtf" }, OriginalFileNames(result.XtfLogs));

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Der Schema-Import war erfolgreich." },
            { "fr", "L'importation du schéma s'est déroulée avec succès." },
            { "it", "L'importazione dello schema è andata a buon fine." },
            { "en", "The schema import was successful." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    public async Task ReportsFailedSchemaImport()
    {
        var process = CreateProcess(failingModel: "model.ili");

        var result = await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.HasCount(0, result.DbFile, "A failed import must not hand on a half written database.");

        // The failed import is a result, not an error: the logs still have to reach the caller.
        CollectionAssert.AreEqual(new[] { "schema-import.log" }, OriginalFileNames(result.ErrorLogs));
        CollectionAssert.AreEqual(new[] { "schema-import-log.xtf" }, OriginalFileNames(result.XtfLogs));
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
        Assert.Contains("Fehler", result.StatusMessage["de"]);
    }

    [TestMethod]
    public async Task PassesLogFilesToClient()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsNotNull(calls.Single().LogFile);
        Assert.IsNotNull(calls.Single().XtfLogFile);
    }

    [TestMethod]
    public async Task PassesModelRepositoryToClient()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.AreSame(modelRepositoryMock.Object, calls.Single().ModelRepository);
    }

    [TestMethod]
    public async Task ThrowsWhenNoModelFilesAreProvided()
    {
        var process = CreateProcess();

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync([], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None));

        Assert.HasCount(0, calls);
    }

    [TestMethod]
    public async Task ExtendsTheGivenDatabase()
    {
        var process = CreateProcess();
        var dbFile = new PipelineFile("TestData/Ilitools/schema.gpkg", "schema.gpkg");

        await process.RunAsync([CreateModelFile("model.ili")], dbFile, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.AreSame(dbFile, calls.Single().InputFile, "The given database has to reach the client unchanged.");
    }

    [TestMethod]
    public async Task ChainsTheImportsOfMultipleModelFiles()
    {
        var process = CreateProcess();
        IPipelineFile[] modelFiles = [CreateModelFile("first.ili"), CreateModelFile("second.ili"), CreateModelFile("third.ili")];

        var result = await process.RunAsync(modelFiles, dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(modelFiles, calls.Select(c => c.ModelFile).ToArray(), "The models have to be imported in the wired order.");

        // Each import extends the database of the previous one, so the last database holds every schema.
        Assert.IsNull(calls[0].InputFile);
        Assert.AreSame(calls[0].OutputFile, calls[1].InputFile);
        Assert.AreSame(calls[1].OutputFile, calls[2].InputFile);
        CollectionAssert.AreEqual(new[] { "schema-1.gpkg", "schema-2.gpkg", "schema.gpkg" }, OriginalFileNames(calls.Select(c => c.OutputFile)));
        Assert.AreSame(calls[2].OutputFile, result.DbFile.Single());

        // One log per model, named after it so they do not overwrite each other.
        CollectionAssert.AreEqual(
            new[] { "schema-import-1-first.log", "schema-import-2-second.log", "schema-import-3-third.log" },
            OriginalFileNames(result.ErrorLogs));
        CollectionAssert.AreEqual(
            new[] { "schema-import-log-1-first.xtf", "schema-import-log-2-second.xtf", "schema-import-log-3-third.xtf" },
            OriginalFileNames(result.XtfLogs));
    }

    [TestMethod]
    public async Task StopsAtTheFirstFailedImport()
    {
        var process = CreateProcess(failingModel: "second.ili");
        IPipelineFile[] modelFiles = [CreateModelFile("first.ili"), CreateModelFile("second.ili"), CreateModelFile("third.ili")];

        var result = await process.RunAsync(modelFiles, dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.HasCount(2, calls, "A model must not be imported into a database whose previous import failed.");
        Assert.HasCount(0, result.DbFile, "The intermediate database of the successful import must not pass as the result.");
        CollectionAssert.AreEqual(new[] { "schema-import-1-first.log", "schema-import-2-second.log" }, OriginalFileNames(result.ErrorLogs));
        CollectionAssert.AreEqual(new[] { "schema-import-log-1-first.xtf", "schema-import-log-2-second.xtf" }, OriginalFileNames(result.XtfLogs));
    }

    [TestMethod]
    public async Task PassesTheModelsAndSrsCodeOfEachRun()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: ["ModelA", "ModelB"], defaultSrsCode: 2056, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, calls[^1].Args.Models?.ToArray());
        Assert.AreEqual(2056, calls[^1].Args.DefaultSrsCode);

        // The models and the srs code belong to one run: a later run without them must not inherit them.
        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsNull(calls[^1].Args.Models);
        Assert.IsNull(calls[^1].Args.DefaultSrsCode);
    }

    [TestMethod]
    public async Task PassesTheSemicolonSeparatedFormOfTheModels()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: ["ModelA; ModelB;"], defaultSrsCode: null, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, calls.Single().Args.Models?.ToArray());
    }

    [TestMethod]
    public async Task PassesTheConfiguredModelReposAndToolVersion()
    {
        var process = CreateProcess(modelDirs: "https://models.example.com/;%XTF_DIR/models", toolVersion: "5.2.0");

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        // The order of the repositories decides which model wins, so it must survive unchanged.
        CollectionAssert.AreEqual(new[] { "https://models.example.com/", "%XTF_DIR/models" }, calls.Single().Args.ModelDirs?.ToArray());
        Assert.AreEqual("5.2.0", calls.Single().Args.ToolVersion);
    }

    [TestMethod]
    public async Task TrimsTheConfiguredModelReposAndToolVersion()
    {
        // Hand written configuration, so blanks around the separator and a trailing one are expected.
        var process = CreateProcess(modelDirs: " https://models.example.com/ ; %XTF_DIR/models ; ", toolVersion: " 5.2.0 ");

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "https://models.example.com/", "%XTF_DIR/models" }, calls.Single().Args.ModelDirs?.ToArray());
        Assert.AreEqual("5.2.0", calls.Single().Args.ToolVersion);
    }

    [TestMethod]
    public async Task PassesNoOptionsWhenOnlyBlanksAreConfigured()
    {
        // Passed on, a blank would reach the wrapper as an empty repository or version name and be rejected.
        var process = CreateProcess(modelDirs: " ; ", toolVersion: "   ");

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        Assert.IsNull(calls.Single().Args.ModelDirs);
        Assert.IsNull(calls.Single().Args.ToolVersion);
    }

    [TestMethod]
    public async Task PassesNoOptionsWhenNoneAreConfigured()
    {
        var process = CreateProcess();

        await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);

        // A schema import does not validate, so it never takes a profile or plugins either.
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
            ["logger"] = Mock.Of<ILogger<Ili2GpkgSchemaImportProcessTest>>(),
        };

        await Ili2GpkgArgsAssert.SetsTheArgumentNamedAfterEachConstructorParameter<Ili2GpkgSchemaImportProcess>(dependencies, async process =>
        {
            calls.Clear();
            await process.RunAsync([CreateModelFile("model.ili")], dbFile: null, models: null, defaultSrsCode: null, CancellationToken.None);
            return calls.Single().Args;
        });
    }

    private static PipelineFile CreateModelFile(string originalFileName)
    {
        return new PipelineFile("TestData/Ilitools/model.ili", originalFileName);
    }

    private static PipelineFileManager CreatePipelineFileManager()
    {
        return new PipelineFileManager(Path.GetTempPath(), "Ili2GpkgSchemaImportProcess");
    }

    private static string[] OriginalFileNames(IEnumerable<IPipelineFile> files)
    {
        return files.Select(f => f.OriginalFileName).ToArray();
    }

    private Ili2GpkgSchemaImportProcess CreateProcess(string? modelDirs = null, string? toolVersion = null, string? failingModel = null)
    {
        this.failingModel = failingModel;
        return new Ili2GpkgSchemaImportProcess(modelDirs, toolVersion, null, null, null, null, null, null, null, null, null, null, null, modelRepositoryMock.Object, ili2GpkgClientMock.Object, CreatePipelineFileManager(), Mock.Of<ILogger<Ili2GpkgSchemaImportProcessTest>>());
    }

    private sealed record SchemaImportCall(Ili2GpkgArgs Args, IPipelineFile ModelFile, IPipelineFile OutputFile, IPipelineFile LogFile, IPipelineFile XtfLogFile, IPipelineFile? InputFile, IPipelineFile? ModelRepository);
}
