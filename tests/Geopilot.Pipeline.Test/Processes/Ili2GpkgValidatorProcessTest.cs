using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Processes.Gpkg;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class Ili2GpkgValidatorProcessTest
{
    private Mock<IIli2GpkgClient> ili2GpkgClientMock;
    private Mock<IPipelineFile> modelRepositoryMock;
    private List<ValidateCall> calls;
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
            .Setup(c => c.ValidateAsync(It.IsAny<Ili2GpkgArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile?>(), It.IsAny<IReadOnlyList<IPipelineFile>?>(), It.IsAny<CancellationToken>()))
            .Callback<Ili2GpkgArgs, IPipelineFile, IPipelineFile, IPipelineFile, IPipelineFile?, IReadOnlyList<IPipelineFile>?, CancellationToken>((args, gpkgFile, logFile, xtfLogFile, modelRepository, _, _) =>
            {
                calls.Add(new ValidateCall(args, gpkgFile, logFile, xtfLogFile, modelRepository));
                if (logContent != null)
                {
                    using var writer = new StreamWriter(logFile.OpenWriteFileStream());
                    writer.Write(logContent);
                }
            })
            .ReturnsAsync(() => new Ili2GpkgResult(success));
        modelRepositoryMock = new Mock<IPipelineFile>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulValidation()
    {
        var process = CreateProcess();
        var dbFile = CreateDbFile();

        var result = await process.RunAsync(dbFile, models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        Assert.IsTrue(result.ValidationSuccessful);
        Assert.AreSame(dbFile, calls.Single().GpkgFile, "The given database has to reach the client unchanged.");
        Assert.AreEqual("validate.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("validate-log.xtf", result.XtfLog.OriginalFileName);

        // The tool writes the xtf log itself, so the file handed to it has to be the one in the result.
        Assert.AreSame(calls.Single().XtfLogFile, result.XtfLog);

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Die Validierung war erfolgreich." },
            { "fr", "La validation a réussi." },
            { "it", "La validazione è riuscita." },
            { "en", "Validation successful." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    public async Task ReportsFailedValidation()
    {
        var process = CreateProcess(success: false);

        var result = await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        Assert.IsFalse(result.ValidationSuccessful);

        // The failed validation is a result, not an error: the logs still have to reach the caller.
        Assert.AreEqual("validate.log", result.ErrorLog.OriginalFileName);
        Assert.AreEqual("validate-log.xtf", result.XtfLog.OriginalFileName);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
        Assert.Contains("Fehler", result.StatusMessage["de"]);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Reported as successful")]
    [DataRow(false, DisplayName = "Reported as failed")]
    public async Task ThrowsWhenACheckWasSkipped(bool success)
    {
        logContent = $"Warning: MandatoryConstraint Model.Topic.Class.Constraint1 {IlitoolsHelper.CheckNotEvaluatedMarker}.\n";
        var process = CreateProcess(pluginIds: "geow-interlis-functions", success: success);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None));

        Assert.Contains("geow-interlis-functions", exception.Message, "The message should name what was configured.");
    }

    [TestMethod]
    public async Task ThrowsWhenTheValidConfigCannotBeResolved()
    {
        // The tool exits like a failed validation, so only its log tells the two apart.
        logContent = $"Error: {IlitoolsHelper.ValidConfigNotFoundMarker("ilidata:validconfig")}\n";
        var process = CreateProcess(modelDirs: "https://models.example.com/", validConfig: "validconfig", success: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None));

        Assert.Contains("ilidata:validconfig", exception.Message);
        Assert.Contains("https://models.example.com/", exception.Message);
        Assert.Contains("ili2gpkg", exception.Message, "The message should name the tool that ran.");
        Assert.Contains("Check the validConfig and modelDirs", exception.Message, "The message should name the setting to fix.");
    }

    [TestMethod]
    public async Task ReportsFailedValidationWhenAnotherFileWasNotFound()
    {
        // Only the line naming the configured reference marks the config as unresolved.
        logContent = "Error: file ilidata:other not found\n";
        var process = CreateProcess(validConfig: "validconfig", success: false);

        var result = await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        Assert.IsFalse(result.ValidationSuccessful);
    }

    [TestMethod]
    public async Task PassesLogFilesToClient()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        Assert.IsNotNull(calls.Single().LogFile);
        Assert.IsNotNull(calls.Single().XtfLogFile);
    }

    [TestMethod]
    public async Task PassesModelRepositoryToClient()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        Assert.AreSame(modelRepositoryMock.Object, calls.Single().ModelRepository);
    }

    [TestMethod]
    public async Task PassesTheSelectionOfEachRun()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: ["ModelA", "ModelB"], topics: ["ModelA.TopicA", "ModelB.TopicB"], baskets: ["b1", "b2"], dataset: ["dataset1"], CancellationToken.None);

        var args = calls.Last().Args;
        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, args.Models?.ToArray());
        CollectionAssert.AreEqual(new[] { "ModelA.TopicA", "ModelB.TopicB" }, args.Topics?.ToArray());
        CollectionAssert.AreEqual(new[] { "b1", "b2" }, args.Baskets?.ToArray());
        Assert.AreEqual("dataset1", args.Dataset);

        // The selection belongs to one run: a later run without one must validate everything again.
        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        Assert.AreEqual(new Ili2GpkgArgs(), calls.Last().Args);
    }

    [TestMethod]
    public async Task PassesTheSemicolonSeparatedFormOfTheSelection()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: ["ModelA; ModelB;"], topics: ["ModelA.TopicA;ModelB.TopicB"], baskets: ["b1;b2"], dataset: ["dataset1;dataset2"], CancellationToken.None);

        var args = calls.Single().Args;
        CollectionAssert.AreEqual(new[] { "ModelA", "ModelB" }, args.Models?.ToArray());
        CollectionAssert.AreEqual(new[] { "ModelA.TopicA", "ModelB.TopicB" }, args.Topics?.ToArray());
        CollectionAssert.AreEqual(new[] { "b1", "b2" }, args.Baskets?.ToArray());
        Assert.AreEqual("dataset1;dataset2", args.Dataset);
    }

    [TestMethod]
    public async Task PassesTheConfiguredStringConfigValues()
    {
        var process = CreateProcess(modelDirs: "https://models.example.com/;%XTF_DIR/models", toolVersion: "5.2.0", pluginIds: "geow-interlis-functions;ngk-so", validConfig: "ilidata:validconfig");

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        // The order of the repositories decides which model wins, so it must survive unchanged.
        var args = calls.Single().Args;
        CollectionAssert.AreEqual(new[] { "https://models.example.com/", "%XTF_DIR/models" }, args.ModelDirs?.ToArray());
        Assert.AreEqual("5.2.0", args.ToolVersion);
        CollectionAssert.AreEqual(new[] { "geow-interlis-functions", "ngk-so" }, args.PluginIds?.ToArray());
        Assert.AreEqual("ilidata:validconfig", args.ValidConfig);
    }

    [TestMethod]
    public async Task TrimsTheConfiguredStringConfigValues()
    {
        // Hand written configuration, so blanks around the separator and a trailing one are expected.
        var process = CreateProcess(modelDirs: " https://models.example.com/ ; %XTF_DIR/models ; ", toolVersion: " 5.2.0 ", pluginIds: " geow-interlis-functions ; ngk-so ; ", validConfig: " validconfig ");

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        var args = calls.Single().Args;
        CollectionAssert.AreEqual(new[] { "https://models.example.com/", "%XTF_DIR/models" }, args.ModelDirs?.ToArray());
        Assert.AreEqual("5.2.0", args.ToolVersion);
        CollectionAssert.AreEqual(new[] { "geow-interlis-functions", "ngk-so" }, args.PluginIds?.ToArray());

        // The valid config is an ilidata reference, so it automatically gets the "ilidata:" prefix.
        Assert.AreEqual("ilidata:validconfig", args.ValidConfig);
    }

    [TestMethod]
    public async Task PassesNoOptionsWhenOnlyBlanksAreConfigured()
    {
        // Passed on, a blank would reach the wrapper as an empty repository, plugin or version name and be rejected.
        var process = CreateProcess(modelDirs: " ; ", toolVersion: "   ", pluginIds: " ; ", validConfig: "   ");

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        var args = calls.Single().Args;
        Assert.IsNull(args.ModelDirs);
        Assert.IsNull(args.ToolVersion);
        Assert.IsNull(args.PluginIds);
        Assert.IsNull(args.ValidConfig);
    }

    [TestMethod]
    public async Task PassesNoOptionsWhenNoneAreConfigured()
    {
        var process = CreateProcess();

        await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);

        // No plugins and no validation configuration have to stay unset, so the tool keeps its own defaults.
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
            ["logger"] = Mock.Of<ILogger<Ili2GpkgValidatorProcessTest>>(),
        };

        await Ili2GpkgArgsAssert.SetsTheArgumentNamedAfterEachConstructorParameter<Ili2GpkgValidatorProcess>(dependencies, async process =>
        {
            calls.Clear();
            await process.RunAsync(CreateDbFile(), models: null, topics: null, baskets: null, dataset: null, CancellationToken.None);
            return calls.Single().Args;
        });
    }

    private static PipelineFile CreateDbFile()
    {
        return new PipelineFile("TestData/Ilitools/data.gpkg", "data.gpkg");
    }

    private static PipelineFileManager CreatePipelineFileManager()
    {
        return new PipelineFileManager(Path.GetTempPath(), "Ili2GpkgValidatorProcess");
    }

    private Ili2GpkgValidatorProcess CreateProcess(string? modelDirs = null, string? toolVersion = null, string? pluginIds = null, string? validConfig = null, bool success = true)
    {
        this.success = success;
        return new Ili2GpkgValidatorProcess(modelDirs, toolVersion, pluginIds, validConfig, null, null, modelRepositoryMock.Object, ili2GpkgClientMock.Object, CreatePipelineFileManager(), Mock.Of<ILogger<Ili2GpkgValidatorProcessTest>>());
    }

    private sealed record ValidateCall(Ili2GpkgArgs Args, IPipelineFile GpkgFile, IPipelineFile LogFile, IPipelineFile XtfLogFile, IPipelineFile? ModelRepository);
}
