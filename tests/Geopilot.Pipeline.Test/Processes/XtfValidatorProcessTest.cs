using Geopilot.Pipeline.Processes.XtfValidation;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfValidatorProcessTest
{
    private Mock<IIlivalidatorClient> ilivalidatorClientMock;
    private IlivalidatorArgs? capturedArgs;
    private IPipelineFile? capturedTransferFile;
    private IPipelineFile? capturedArchive;
    private IReadOnlyList<IPipelineFile>? capturedModelFiles;

    [TestInitialize]
    public void SetUp()
    {
        capturedArgs = null;
        capturedTransferFile = null;
        capturedArchive = null;
        capturedModelFiles = null;
        ilivalidatorClientMock = new Mock<IIlivalidatorClient>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulValidation()
    {
        var process = CreateProcess(validationProfile: null, modelDirs: null, success: true);

        var result = await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.IsTrue(result.ValidationSuccessful);
        Assert.AreEqual("RoadsExdm2ien.xtf", capturedTransferFile?.OriginalFileName);
        Assert.AreEqual("errorLog.log", result.ErrorLog?.OriginalFileName);
        Assert.AreEqual("xtfLog.xtf", result.XtfLog?.OriginalFileName);

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
    public async Task ThrowsWhenTheProfileCannotBeResolved()
    {
        // The tool exits like a failed validation, so only its log tells the two apart.
        var log = $"Info: dataFile <file1.xtf>\nError: {XtfValidatorProcess.MetaConfigNotFoundMarker} <ilidata:PROFILE-A>\n";
        var process = CreateProcess("PROFILE-A", "https://models.example.com/", success: false, logContent: log);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateTransferFile(), [], CancellationToken.None));

        Assert.Contains("ilidata:PROFILE-A", exception.Message);
        Assert.Contains("https://models.example.com/", exception.Message);
    }

    [TestMethod]
    public async Task ReportsFailedValidation()
    {
        // A log without the marker is an ordinary failed validation and must not throw.
        var process = CreateProcess("PROFILE-A", modelDirs: null, success: false, logContent: "Error: Attribute Hoehengenauigkeit requires a value\n");

        var result = await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.IsFalse(result.ValidationSuccessful);

        // The failed validation is a result, not an error: the logs still have to reach the caller.
        Assert.AreEqual("errorLog.log", result.ErrorLog?.OriginalFileName);
        Assert.AreEqual("xtfLog.xtf", result.XtfLog?.OriginalFileName);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
        Assert.Contains("Fehler", result.StatusMessage["de"]);
    }

    [TestMethod]
    public async Task PassesModelReposAndProfileToTheTool()
    {
        var process = CreateProcess("PROFILE-A", "https://models.example.com/;%ITF_DIR", success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.AreEqual("ilidata:PROFILE-A", capturedArgs?.MetaConfig);

        // The order of the repositories decides which model wins, so it must survive unchanged.
        string[] expectedModelDirs = ["https://models.example.com/", "%ITF_DIR"];
        CollectionAssert.AreEqual(expectedModelDirs, capturedArgs?.ModelDirs?.ToList());
    }

    [TestMethod]
    public async Task ThrowsWhenACheckWasSkipped()
    {
        // The dangerous shape: the tool reports success although it never evaluated the constraint, which is what
        // a missing plugin looks like. A green result for a check that did not run must not reach the delivery.
        var log = "Info: validate mandatory constraint Model.Topic.Class.Constraint1...\n"
            + $"Warning: line 12: Model.Topic.Class: tid 1: MandatoryConstraint Model.Topic.Class.Constraint1 of Model.Topic.Class {XtfValidatorProcess.CheckNotEvaluatedMarker}.\n";
        var process = CreateProcess(null, null, success: true, logContent: log, pluginIds: "geow-interlis-functions");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateTransferFile(), [], CancellationToken.None));

        Assert.Contains("geow-interlis-functions", exception.Message, "The message should name what was configured.");
    }

    [TestMethod]
    public async Task ThrowsWhenACheckWasSkippedWithoutAnyPluginConfigured()
    {
        // The likelier misconfiguration: nobody configured the plugin the model needs. Reported the same way,
        // because the result says just as little about that check.
        var log = $"Warning: MandatoryConstraint Model.Topic.Class.Constraint1 of Model.Topic.Class {XtfValidatorProcess.CheckNotEvaluatedMarker}.\n";
        var process = CreateProcess(null, null, success: true, logContent: log);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateTransferFile(), [], CancellationToken.None));

        Assert.Contains("none", exception.Message);
    }

    [TestMethod]
    public async Task ThrowsWhenASetConstraintWasSkipped()
    {
        // The tool words the skip once per constraint type and twice generically, so the marker is the common
        // substring and not one message. A set constraint is one of the wordings that a search for
        // MandatoryConstraint would miss.
        var log = $"Warning: Function in set constraint Model.Topic.Class.Constraint2 {XtfValidatorProcess.CheckNotEvaluatedMarker}.\n";
        var process = CreateProcess(null, null, success: true, logContent: log);

        await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateTransferFile(), [], CancellationToken.None));
    }

    [TestMethod]
    public async Task PassesTheConfiguredPluginsAsIds()
    {
        var process = CreateProcess(null, null, success: true, pluginIds: " geow-interlis-functions ; ngk-so ; ");

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        // Hand written configuration, so blanks around the separator and a trailing one are expected.
        string[] expectedPluginIds = ["geow-interlis-functions", "ngk-so"];
        CollectionAssert.AreEqual(expectedPluginIds, capturedArgs?.PluginIds?.ToList());
    }

    [TestMethod]
    public async Task PassesNoPluginsWhenNoneAreConfigured()
    {
        var process = CreateProcess(null, null, success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        // No selection has to stay no selection: the wrapper then leaves --plugins unset, which is what keeps the
        // tool on its own default instead of loading something the pipeline never asked for.
        Assert.IsNull(capturedArgs?.PluginIds);
    }

    [TestMethod]
    public async Task TrimsTheConfiguredModelRepositories()
    {
        // Hand written configuration, so blanks around the separator and a trailing one are expected.
        var process = CreateProcess(null, " https://models.example.com/ ; %ITF_DIR ; ", success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        string[] expectedModelDirs = ["https://models.example.com/", "%ITF_DIR"];
        CollectionAssert.AreEqual(expectedModelDirs, capturedArgs?.ModelDirs?.ToList());
    }

    [TestMethod]
    public async Task KeepsAnAlreadyPrefixedProfile()
    {
        var process = CreateProcess("ilidata:PROFILE-A", modelDirs: null, success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.AreEqual("ilidata:PROFILE-A", capturedArgs?.MetaConfig);
    }

    [TestMethod]
    public async Task OmitsTheProfileWhenNoneIsConfigured()
    {
        var process = CreateProcess("   ", modelDirs: null, success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.IsNull(capturedArgs?.MetaConfig, "A blank profile must not reach the tool as an empty meta config.");
        Assert.IsNull(capturedArgs?.ModelDirs);

        // The check service applied this option to every validation, so an unconfigured pipeline has to keep it.
        Assert.IsTrue(capturedArgs?.AllObjectsAccessible, "Missing configuration must not weaken the validation.");
    }

    [TestMethod]
    public async Task PassesTheModelRepositoryArchiveOn()
    {
        var archive = new PipelineFile(Path.Combine("TestData", "ModelRepository", "model-repository.zip"), "model-repository.zip");
        var process = CreateProcess(null, "%ITF_DIR", success: true, modelRepository: archive);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.AreSame(archive, capturedArchive, "The configured archive has to reach the client unchanged.");
    }

    [TestMethod]
    public async Task SendsNoArchiveWhenNoneIsConfigured()
    {
        var process = CreateProcess(null, modelDirs: null, success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.IsNull(capturedArchive);
    }

    [TestMethod]
    public async Task AcceptsTurningOffAllObjectsAccessible()
    {
        var process = CreateProcess(null, modelDirs: null, success: true, allObjectsAccessible: false);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.IsFalse(capturedArgs?.AllObjectsAccessible);
    }

    [TestMethod]
    public async Task ForwardsTheDeliveredModelFilesVerbatim()
    {
        var process = CreateProcess(null, "%ITF_DIR", success: true);

        // The pipeline decides what counts as a model (typically through a matcher), so the process has to hand
        // the wired files on unchanged instead of filtering them itself.
        IPipelineFile[] delivered =
        [
            new PipelineFile("TestData/Ilitools/model.ili", "delivered.ili"),
            new PipelineFile("TestData/Ilitools/model.ili", "second.ili"),
        ];

        await process.RunAsync(CreateTransferFile(), delivered, CancellationToken.None);

        Assert.IsNotNull(capturedModelFiles);
        CollectionAssert.AreEqual(delivered, capturedModelFiles.ToList(), "The wired model files have to reach the client unchanged and in order.");
    }

    [TestMethod]
    public async Task SendsNoModelFilesWhenNoneAreDelivered()
    {
        var process = CreateProcess(null, modelDirs: null, success: true);

        await process.RunAsync(CreateTransferFile(), [], CancellationToken.None);

        Assert.IsNotNull(capturedModelFiles);
        Assert.HasCount(0, capturedModelFiles);
    }

    private static PipelineFile CreateTransferFile()
    {
        return new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
    }

    private XtfValidatorProcess CreateProcess(string? validationProfile, string? modelDirs, bool success, bool? allObjectsAccessible = null, string? logContent = null, IPipelineFile? modelRepository = null, string? pluginIds = null)
    {
        ilivalidatorClientMock
            .Setup(c => c.ValidateAsync(It.IsAny<IlivalidatorArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile?>(), It.IsAny<IReadOnlyList<IPipelineFile>?>(), It.IsAny<CancellationToken>()))
            .Callback<IlivalidatorArgs, IPipelineFile, IPipelineFile, IPipelineFile, IPipelineFile?, IReadOnlyList<IPipelineFile>?, CancellationToken>((args, transferFile, logFile, _, archive, modelFiles, _) =>
            {
                capturedArgs = args;
                capturedTransferFile = transferFile;
                capturedArchive = archive;
                capturedModelFiles = modelFiles;

                if (logContent != null)
                {
                    using var stream = logFile.OpenWriteFileStream();
                    using var writer = new StreamWriter(stream);
                    writer.Write(logContent);
                }
            })
            .ReturnsAsync(new IlivalidatorResult(success));

        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "XtfValidatorProcess");
        return new XtfValidatorProcess(validationProfile, modelDirs, allObjectsAccessible, pluginIds, modelRepository, ilivalidatorClientMock.Object, pipelineFileManager, Mock.Of<ILogger<XtfValidatorProcessTest>>());
    }
}
