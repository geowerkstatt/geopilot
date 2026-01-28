using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using System;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
    [TestMethod]
    public void RunTwoStepPipeline()
    {
        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");
        var pipeline = factory.CreatePipeline("two_steps");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(2, pipeline.Steps);

        using var fileHandle = CreateTestFileHandle(@"TestData\UploadFiles\simple.xtf");
        var context = pipeline.Run(fileHandle);

        var uploadStepData = new StepResult()
        {
            State = StepState.Success,
            Outputs = new Dictionary<string, StepOutput>(),
        };
        var stepResults = new Dictionary<string, StepResult>()
        {
            { "upload", uploadStepData },
        };

        Assert.AreEqual(stepResults, context.StepResults, "step results not as expected");
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
