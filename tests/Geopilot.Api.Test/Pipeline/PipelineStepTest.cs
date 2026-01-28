using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineStepTest
{
    [TestMethod]
    public void Run()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "upload",
                Take = "xtf_file",
                As = "file_to_validate",
            },
        };
        var outputConfigs = new List<OutputConfig>
        {
            new OutputConfig
            {
                Take = "error_log",
                As = "my_output",
                Action = OutputAction.IGNORE,
            },
        };
        var uploadStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "xtf_file", new StepOutput { Action = OutputAction.IGNORE, Data = "some_data" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };
        var processData = new ProcessData();
        processData.AddData("error_log", new ProcessDataPart("some_data"));

        var processMock = new Mock<IPipelineProcess>();
        processMock.Setup(p => p.Run(It.IsAny<ProcessData>())).Returns(processData);

        var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock.Object);

        var stepResult = pipelineStep.Run(pipelineContext);

        Assert.IsNotNull(stepResult);

        // Verify that process.Run was called exactly once and with the correct ProcessData
        processMock.Verify(
            p => p.Run(It.Is<ProcessData>(pd =>
                pd.Data.Count == 1 &&
                pd.Data.ContainsKey("file_to_validate") &&
                pd.Data["file_to_validate"].Data.Equals("some_data"))),
            Times.Once());

        // Assert that the returned StepResult contains the correct content
        Assert.HasCount(1, stepResult.Outputs);
        Assert.IsTrue(stepResult.Outputs.ContainsKey("my_output"));
        Assert.AreEqual(OutputAction.IGNORE, stepResult.Outputs["my_output"].Action);
        Assert.AreEqual("some_data", stepResult.Outputs["my_output"].Data);
    }
}
