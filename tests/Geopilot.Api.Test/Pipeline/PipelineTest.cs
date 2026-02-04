using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
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

    [TestMethod(DisplayName = "Pipeline State Test")]
    [DataRow(PipelineState.Pending, new[] { StepState.Pending, StepState.Pending })]
    [DataRow(PipelineState.Running, new[] { StepState.Running, StepState.Pending })]
    [DataRow(PipelineState.Pending, new StepState[0])]
    [DataRow(PipelineState.Failed, new[] { StepState.Success, StepState.Failed, StepState.Pending })]
    public void PipelineStateTest(PipelineState expectedState, IEnumerable<StepState> stepStates)
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };
        var inputConfigs = new List<InputConfig>();
        var outputConfigs = new List<OutputConfig>();

        var steps = stepStates
            .Select(s =>
            {
                var step = new Mock<IPipelineStep>();
                step.SetupProperty(s => s.State, s);
                return step.Object;
            })
            .ToList();

        var pipelineParameters = new PipelineParametersConfig() { UploadStep = "upload", Mappings = new List<FileMappingsConfig>() };

        var pipeline = new Api.Pipeline.Pipeline("test_pipeline", pipelineDisplayName, steps, pipelineParameters);

        Assert.AreEqual(expectedState, pipeline.State);
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        return PipelineFactory.FromFile(path);
    }

    private FileHandle CreateTestFileHandle(string file)
    {
        var tempFilePath = Path.GetTempFileName();
        var stream = File.Open(file, FileMode.Open, System.IO.FileAccess.Read);
        return new FileHandle(file, stream);
    }
}
