using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Processes.XtfValidation;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineIntegrationTest
{
    private Mock<IIlivalidatorClient> ilivalidatorClientMock;
    private Mock<IOptions<PipelineOptions>> pipelineOptionsMock;
    private PipelineProcessFactory pipelineProcessFactory;
    private Mock<ILogger> loggerMock;
    private Mock<ILoggerFactory> loggerFactoryMock;

    [TestInitialize]
    public void SetUp()
    {
        ilivalidatorClientMock = new Mock<IIlivalidatorClient>();

        var pipelineOptions = new PipelineOptions()
        {
            Definition = "myPipeline.yaml",
            Plugins = new List<string>(),
            ProcessConfigs = new Dictionary<string, Parameterization>(),
        };

        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        loggerMock = new Mock<ILogger>();
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        var ilitoolsOptionsMock = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptionsMock.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });
        this.pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        pipelineProcessFactory?.Dispose();
    }

    [TestMethod]
    public async Task RunTwoStepPipelineRoadsExdm2ien()
    {
        var matcherStepId = "matcher";
        var validationStepId = "validation";
        var zipPackageStepId = "zip_package";
        var xtfFileAttribute = "XtfFiles";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf"),
                new PipelineFile("TestData/UploadFiles/iseltwald_gwp_be13_1.xtf", "iseltwald_gwp_be13_1.xtf"),
            };
        using var pipeline = factory.CreatePipeline("two_steps_roadsexdm2ien", Guid.NewGuid());

        SetUpIlivalidatorClient(validationSuccessful: true);
        InjectIlivalidatorClient(pipeline);

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(3, pipeline.Steps);

        var context = await pipeline.Run(pipelineFiles, CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[1].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[2].State);

        // Assert if uploaded file was correctly added to PipelineContext
        var stepResults = context.StepResults;
        var matcherStepResult = stepResults[matcherStepId];
        var xtfFileData = matcherStepResult.ExtractProperty(xtfFileAttribute);

        Assert.IsNotNull(xtfFileData);
        var xtfFiles = xtfFileData as IPipelineFile[];
        Assert.HasCount(1, xtfFiles);
        Assert.AreEqual("RoadsExdm2ien.xtf", xtfFiles[0].OriginalFileName);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(3, stepResults);

        Assert.IsTrue(stepResults.ContainsKey(validationStepId));
        var validationSetpResult = stepResults[validationStepId];
        Assert.IsNotNull(validationSetpResult.ExtractProperty("XtfLog"), "validation should produce an XtfLog");

        Assert.IsTrue(stepResults.ContainsKey(zipPackageStepId));
        var zipPackageStepResult = stepResults[zipPackageStepId];
        var zipFile = zipPackageStepResult.ExtractProperty("ZipPackage") as IPipelineFile;
        Assert.IsNotNull(zipFile, "No ZIP file in output");
        Assert.AreEqual("myPersonalZipArchive.zip", zipFile.OriginalFileName, "ZIP file has not the expected name");

        // The zip step aggregates three prior outputs (the matched XTF file and both validation logs)
        // into its single array parameter. Asserting the entry count guards against a regression where
        // only the first source is packaged.
        using var zipStream = await zipFile.OpenReadAsync();
        using var zipArchive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
        Assert.HasCount(3, zipArchive.Entries, "ZIP should contain the matched XTF file plus both validation logs");

        pipelineOptionsMock.Verify();
    }

    [TestMethod]
    public async Task RunTwoStepPipelineAmbiguousModel()
    {
        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien1.xtf"),
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien2.xtf"),
            };
        using var pipeline = factory.CreatePipeline("two_steps_roadsexdm2ien", Guid.NewGuid());

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(3, pipeline.Steps);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipeline.Run(pipelineFiles, CancellationToken.None));
        Assert.IsNotNull(exception);
        Assert.AreEqual("Input for parameter 'transferFile' resolved to 2 values, but a single value is required.", exception.Message);
    }

    [TestMethod]
    public async Task RunTwoStepPipelineSkipsValidationWhenMultipleMatches()
    {
        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien1.xtf"),
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien2.xtf"),
            };
        using var pipeline = factory.CreatePipeline("two_steps_skip_validation", Guid.NewGuid());

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(3, pipeline.Steps);

        var context = await pipeline.Run(pipelineFiles, CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Skipped, pipeline.Steps[1].State);
        Assert.AreEqual(StepState.Skipped, pipeline.Steps[2].State);

        // Assert matcher step produced 2 matched files
        var stepResults = context.StepResults;
        var matcherStepResult = stepResults["matcher"];
        var xtfFileData = matcherStepResult.ExtractProperty("XtfFiles");
        Assert.IsNotNull(xtfFileData);
        var xtfFiles = xtfFileData as IPipelineFile[];
        Assert.HasCount(2, xtfFiles);
    }

    [TestMethod]
    public async Task RunPipelineWithFileReference()
    {
        var resourcesDirectory = Path.Combine("TestData", "Resources");

        PipelineFactory factory = CreatePipelineFactory("fileReferencePipeline", resourcesDirectory);

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        using var pipeline = factory.CreatePipeline("file_reference", Guid.NewGuid());

        var context = await pipeline.Run(new List<IPipelineFile>(), CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);

        var zipFile = context.StepResults["zip_package"].ExtractProperty("ZipPackage") as IPipelineFile;
        Assert.IsNotNull(zipFile, "No ZIP file in output");

        using var zipStream = await zipFile.OpenReadAsync();
        using var zipArchive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
        Assert.HasCount(1, zipArchive.Entries, "ZIP should contain the referenced resource file");
        Assert.AreEqual("sample.txt", zipArchive.Entries[0].Name);
    }

    [TestMethod]
    public async Task RunPipelineWithUploadReference()
    {
        PipelineFactory factory = CreatePipelineFactory("uploadReferencePipeline");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new List<IPipelineFile>
        {
            new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf"),
        };
        using var pipeline = factory.CreatePipeline("upload_reference", Guid.NewGuid());

        var context = await pipeline.Run(pipelineFiles, CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);

        var matchedData = context.StepResults["matcher"].ExtractProperty("XtfFiles");
        var matched = matchedData as IPipelineFile[];
        Assert.IsNotNull(matched, "matcher did not output XtfFiles");
        Assert.HasCount(1, matched);
        Assert.AreEqual("RoadsExdm2ien.xtf", matched[0].OriginalFileName);
    }

    // The validation logs are the input of the zip step, so the client double has to produce them the way the
    // real one does: written into the files the process handed it.
    private void SetUpIlivalidatorClient(bool validationSuccessful)
    {
        ilivalidatorClientMock
            .Setup(c => c.ValidateAsync(It.IsAny<IlivalidatorArgs>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile>(), It.IsAny<IPipelineFile?>(), It.IsAny<CancellationToken>()))
            .Callback<IlivalidatorArgs, IPipelineFile, IPipelineFile, IPipelineFile, IPipelineFile?, CancellationToken>((_, _, logFile, xtfLogFile, _, _) =>
            {
                CopyInto("TestData/DownloadFiles/ilicop/log.log", logFile);
                CopyInto("TestData/DownloadFiles/ilicop/log.xtf", xtfLogFile);
            })
            .ReturnsAsync(new IlivalidatorResult(validationSuccessful));
    }

    private void InjectIlivalidatorClient(IPipeline pipeline)
    {
        var clientField = typeof(XtfValidatorProcess).GetField("ilivalidatorClient", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(clientField, "XtfValidatorProcess no longer holds the ilivalidator client in a field named <ilivalidatorClient>.");

        foreach (var process in pipeline.Steps.Select(s => s.Process).OfType<XtfValidatorProcess>())
        {
            clientField.SetValue(process, ilivalidatorClientMock.Object);
        }
    }

    private static void CopyInto(string sourcePath, IPipelineFile target)
    {
        using var source = File.OpenRead(sourcePath);
        using var destination = target.OpenWriteFileStream();
        source.CopyTo(destination);
    }

    private PipelineFactory CreatePipelineFactory(string filename, string? resourcesDirectory = null)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        string pipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline");

        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(this.pipelineProcessFactory)
            .LoggerFactory(this.loggerFactoryMock.Object)
            .PipelineTempDirectory(pipelineDirectory)
            .ResourcesDirectory(resourcesDirectory)
            .Build();
    }
}
