using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineStepTest
{
    private class MockPipelineProcess
    {
        public MockPipelineProcess(ProcessData outputData)
        {
            this.outputData = outputData;
        }

        private ProcessData outputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<ProcessData> RunAsync(string data)
        {
            NumberOfRunInvoced++;
            return this.outputData;
        }
    }

    private class MockPipelineProcessException
    {
        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<ProcessData> RunAsync(string data)
        {
            ++NumberOfRunInvoced;
            throw new InvalidOperationException("Test exception during process run.");
        }
    }

    [TestMethod]
    public void SuccessfullStepRun()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "upload",
                Take = "xtf_file",
                As = "data",
            },
        };
        var outputConfigs = new List<OutputConfig>
        {
            new OutputConfig
            {
                Take = "error_log",
                As = "my_output",
                Action = new HashSet<OutputAction>(),
            },
        };
        var uploadStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "xtf_file", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some_data" } },
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

        var processMock = new MockPipelineProcess(processData);

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext)).GetAwaiter().GetResult();

        Assert.IsNotNull(stepResult);

        Assert.AreEqual(StepState.Success, pipelineStep.State);

        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");

        // Assert that the returned StepResult contains the correct content
        Assert.HasCount(1, stepResult.Outputs);
        Assert.IsTrue(stepResult.Outputs.ContainsKey("my_output"));
        Assert.IsEmpty(stepResult.Outputs["my_output"].Action);
        Assert.AreEqual("some_data", stepResult.Outputs["my_output"].Data);
    }

    [TestMethod]
    public void ContextHasNoReferencingStepRusult()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "invalid_upload_reference",
                Take = "xtf_file",
                As = "data",
            },
        };
        var outputConfigs = new List<OutputConfig>
        {
            new OutputConfig
            {
                Take = "error_log",
                As = "my_output",
                Action = new HashSet<OutputAction>(),
            },
        };
        var uploadStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "xtf_file", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some_data" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };

        var processMock = new MockPipelineProcess(new ProcessData());

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext)).GetAwaiter().GetResult();

        Assert.IsEmpty(stepResult.Outputs);

        Assert.AreEqual(StepState.Failed, pipelineStep.State);

        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public void ContextHasNoReferencingStepOutput()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "upload",
                Take = "xtf_file_wrong_reference",
                As = "file_to_validate",
            },
        };
        var outputConfigs = new List<OutputConfig>
        {
            new OutputConfig
            {
                Take = "error_log",
                As = "my_output",
                Action = new HashSet<OutputAction>(),
            },
        };
        var uploadStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "xtf_file", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some_data" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };

        var processMock = new MockPipelineProcess(new ProcessData());

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext)).GetAwaiter().GetResult();

        Assert.IsEmpty(stepResult.Outputs);

        Assert.AreEqual(StepState.Failed, pipelineStep.State);

        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public void ExceptionDuringProcessRun()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "upload",
                Take = "xtf_file",
                As = "data",
            },
        };
        var outputConfigs = new List<OutputConfig>
        {
            new OutputConfig
            {
                Take = "error_log",
                As = "my_output",
                Action = new HashSet<OutputAction>(),
            },
        };
        var uploadStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "xtf_file", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some_data" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };

        var processMock = new MockPipelineProcessException();

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext)).GetAwaiter().GetResult();

        Assert.IsEmpty(stepResult.Outputs);

        Assert.AreEqual(StepState.Failed, pipelineStep.State);

        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public void StepResultCouldNotBeCreated()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "upload",
                Take = "xtf_file",
                As = "data",
            },
        };
        var outputConfigs = new List<OutputConfig>
        {
            new OutputConfig
            {
                Take = "error_log_wrong_reference",
                As = "my_output",
                Action = new HashSet<OutputAction>(),
            },
        };
        var uploadStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "xtf_file", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some_data" } },
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

        var processMock = new MockPipelineProcess(processData);

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext)).GetAwaiter().GetResult();

        Assert.IsEmpty(stepResult.Outputs);

        Assert.AreEqual(StepState.Failed, pipelineStep.State);

        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }
}
