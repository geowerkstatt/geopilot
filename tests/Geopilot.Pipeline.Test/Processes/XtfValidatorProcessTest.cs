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

    [TestInitialize]
    public void SetUp()
    {
        capturedArgs = null;
        capturedTransferFile = null;
        ilivalidatorClientMock = new Mock<IIlivalidatorClient>();
    }

    [TestMethod]
    public async Task ReportsSuccessfulValidation()
    {
        var process = CreateProcess(validationProfile: null, modelDirs: null, success: true);

        var result = await process.RunAsync(CreateTransferFile(), CancellationToken.None);

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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateTransferFile(), CancellationToken.None));

        Assert.Contains("ilidata:PROFILE-A", exception.Message);
        Assert.Contains("https://models.example.com/", exception.Message);
    }

    [TestMethod]
    public async Task ReportsFailedValidation()
    {
        // A log without the marker is an ordinary failed validation and must not throw.
        var process = CreateProcess("PROFILE-A", modelDirs: null, success: false, logContent: "Error: Attribute Hoehengenauigkeit requires a value\n");

        var result = await process.RunAsync(CreateTransferFile(), CancellationToken.None);

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

        await process.RunAsync(CreateTransferFile(), CancellationToken.None);

        Assert.AreEqual("ilidata:PROFILE-A", capturedArgs?.MetaConfig);

        // The order of the repositories decides which model wins, so it must survive unchanged.
        Assert.AreEqual(2, capturedArgs?.ModelDirs?.Count);
        Assert.AreEqual("https://models.example.com/", capturedArgs?.ModelDirs?[0]);
        Assert.AreEqual("%ITF_DIR", capturedArgs?.ModelDirs?[1]);
    }

    [TestMethod]
    public async Task TrimsTheConfiguredModelRepositories()
    {
        // Hand written configuration, so blanks around the separator and a trailing one are expected.
        var process = CreateProcess(null, " https://models.example.com/ ; %ITF_DIR ; ", success: true);

        await process.RunAsync(CreateTransferFile(), CancellationToken.None);

        Assert.AreEqual(2, capturedArgs?.ModelDirs?.Count);
        Assert.AreEqual("https://models.example.com/", capturedArgs?.ModelDirs?[0]);
        Assert.AreEqual("%ITF_DIR", capturedArgs?.ModelDirs?[1]);
    }

    [TestMethod]
    public async Task KeepsAnAlreadyPrefixedProfile()
    {
        var process = CreateProcess("ilidata:PROFILE-A", modelDirs: null, success: true);

        await process.RunAsync(CreateTransferFile(), CancellationToken.None);

        Assert.AreEqual("ilidata:PROFILE-A", capturedArgs?.MetaConfig);
    }

    [TestMethod]
    public async Task OmitsTheProfileWhenNoneIsConfigured()
    {
        var process = CreateProcess("   ", modelDirs: null, success: true);

        await process.RunAsync(CreateTransferFile(), CancellationToken.None);

        Assert.IsNull(capturedArgs?.MetaConfig, "A blank profile must not reach the tool as an empty meta config.");
        Assert.IsNull(capturedArgs?.ModelDirs);

        // The check service applied this option to every validation, so an unconfigured pipeline has to keep it.
        Assert.IsTrue(capturedArgs?.AllObjectsAccessible, "Missing configuration must not weaken the validation.");
    }

    [TestMethod]
    public async Task AcceptsTurningOffAllObjectsAccessible()
    {
        var process = CreateProcess(null, modelDirs: null, success: true, allObjectsAccessible: false);

        await process.RunAsync(CreateTransferFile(), CancellationToken.None);

        Assert.IsFalse(capturedArgs?.AllObjectsAccessible);
    }

    private static PipelineFile CreateTransferFile()
    {
        return new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
    }

    private XtfValidatorProcess CreateProcess(string? validationProfile, string? modelDirs, bool success, bool? allObjectsAccessible = null, string? logContent = null)
    {
        ilivalidatorClientMock
            .Setup(c => c.ValidateAsync(It.IsAny<IlivalidatorArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<CancellationToken>()))
            .Callback<IlivalidatorArgs, IPipelineFile, IPipelineFile, IPipelineFile, CancellationToken>((args, transferFile, logFile, _, _) =>
            {
                capturedArgs = args;
                capturedTransferFile = transferFile;

                if (logContent != null)
                {
                    using var stream = logFile.OpenWriteFileStream();
                    using var writer = new StreamWriter(stream);
                    writer.Write(logContent);
                }
            })
            .ReturnsAsync(new IlivalidatorResult(success));

        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "XtfValidatorProcess");
        return new XtfValidatorProcess(validationProfile, modelDirs, allObjectsAccessible, ilivalidatorClientMock.Object, pipelineFileManager, Mock.Of<ILogger<XtfValidatorProcessTest>>());
    }
}
