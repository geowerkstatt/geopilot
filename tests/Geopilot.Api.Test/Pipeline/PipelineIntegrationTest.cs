using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using WireMock.Server;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineIntegrationTest
{
    private IConfiguration configuration;
    private WireMockServer server;

    [TestInitialize]
    public void SetUp()
    {
        server = WireMockServer.Start();
        var inMemorySettings = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Validation:InterlisCheckServiceUrl", server.Url),
        };

        #pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        this.configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        #pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
    }

    [TestCleanup]
    public void Cleanup()
    {
        server.Stop();
        server.Dispose();
    }

    [TestMethod]
    public void RunTwoStepPipeline()
    {
        var uploadStepId = "upload";
        var validationStepId = "validation";
        var dummyStepId = "dummy";
        var uploadedFileAttribute = "ili_file";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");
        var pipeline = factory.CreatePipeline("two_steps");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(2, pipeline.Steps);

        using var fileHandle = CreateTestFileHandle("TestData/UploadFiles/RoadsExdm2ien.xtf");
        var context = pipeline.Run(fileHandle);

        Assert.AreEqual(PipelineState.Success, pipeline.State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[1].State);

        // Assert if uploaded file was correctly added to PipelineContext
        var stepResults = context.StepResults;

        Assert.IsTrue(stepResults.ContainsKey(uploadStepId));
        var uploadStepResult = context.StepResults[uploadStepId];
        Assert.HasCount(1, uploadStepResult.Outputs, "upload step has not the expected number of data");
        Assert.IsTrue(uploadStepResult.Outputs.ContainsKey(uploadedFileAttribute));
        var uploadedFileStepOutput = uploadStepResult.Outputs[uploadedFileAttribute];

        Assert.IsNotNull(uploadedFileStepOutput.Data);
        var uploadedFile = uploadedFileStepOutput.Data as FileHandle;
        Assert.IsNotNull(uploadedFile);
        Assert.AreEqual(fileHandle.FileName, uploadedFile.FileName);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(3, stepResults);
        Assert.IsTrue(stepResults.ContainsKey(validationStepId));
        var validationSetpResult = stepResults[validationStepId];
        Assert.HasCount(2, validationSetpResult.Outputs, "validation step has not the expected number of data");

        Assert.IsTrue(stepResults.ContainsKey(dummyStepId));
        var dummyStepResult = stepResults[dummyStepId];
        Assert.HasCount(1, dummyStepResult.Outputs, "dummy step has not the expected number of data");
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        using CancellationTokenSource cancellationToken = new CancellationTokenSource();
        return PipelineFactory.PipelineFactoryBuilder
            .Builder()
            .File(path)
            .Configuration(configuration)
            .CancellationToken(cancellationToken.Token)
            .Build();
    }

    private FileHandle CreateTestFileHandle(string file)
    {
        var stream = File.Open(file, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        return new FileHandle(file, stream);
    }
}
