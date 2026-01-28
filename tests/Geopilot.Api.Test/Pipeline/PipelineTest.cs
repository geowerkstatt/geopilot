using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Moq;
using System;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
    [TestMethod]
    public void RunTwoStepPipeline()
    {
        var uploadStepName = "upload";
        var uploadedFileAttribute = "ili_file";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");
        var pipeline = factory.CreatePipeline("two_steps");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(2, pipeline.Steps);

        using var fileHandle = CreateTestFileHandle("TestData/UploadFiles/RoadsExdm2ien.xtf");
        var context = pipeline.Run(fileHandle);

        // Assert if uploaded file was correctly added to PipelineContext
        var stepResults = context.StepResults;
        Assert.IsTrue(stepResults.ContainsKey(uploadStepName));
        var uploadStepResult = context.StepResults[uploadStepName];
        Assert.HasCount(1, uploadStepResult.Outputs);
        Assert.IsTrue(uploadStepResult.Outputs.ContainsKey(uploadedFileAttribute));
        var uploadedFileStepOutput = uploadStepResult.Outputs[uploadedFileAttribute];
        Assert.IsNotNull(uploadedFileStepOutput.Data);
        var uploadedFile = uploadedFileStepOutput.Data as FileHandle;
        Assert.IsNotNull(uploadedFile);
        Assert.AreEqual(fileHandle.FileName, uploadedFile.FileName);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(3, stepResults);
        Assert.IsTrue(stepResults.ContainsKey("validation"));
        Assert.IsNotNull(stepResults["validation"]);
        Assert.IsTrue(stepResults.ContainsKey("dummy"));
        Assert.IsNotNull(stepResults["dummy"]);
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
