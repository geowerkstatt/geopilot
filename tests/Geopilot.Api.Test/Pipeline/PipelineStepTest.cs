using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineStepTest
{
    private class MockPipelineProcessSingleInput
    {
        public MockPipelineProcessSingleInput(Dictionary<string, object> outputData)
        {
            this.outputData = outputData;
        }

        private Dictionary<string, object> outputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string data, CancellationToken cancellationToken)
        {
            Assert.IsNotNull(data);
            Assert.IsNotNull(cancellationToken);
            NumberOfRunInvoced++;
            return this.outputData;
        }
    }

    private class MockPipelineProcessArrayInput
    {
        public MockPipelineProcessArrayInput(Dictionary<string, object> outputData)
        {
            this.outputData = outputData;
        }

        private Dictionary<string, object> outputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string[] data, CancellationToken cancellationToken)
        {
            Assert.IsNotNull(data);
            Assert.IsNotEmpty(data, "data expected");
            Assert.IsNotNull(cancellationToken);
            NumberOfRunInvoced++;
            return this.outputData;
        }
    }

    private class MockPipelineProcessManyDifferentInputTypesInput
    {
        public MockPipelineProcessManyDifferentInputTypesInput(Dictionary<string, object> outputData)
        {
            this.outputData = outputData;
        }

        private Dictionary<string, object> outputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string[] srtingData, int intData, bool booleanData, double doubleData, CancellationToken cancellationToken)
        {
            NumberOfRunInvoced++;
            Assert.IsNotNull(srtingData);
            Assert.IsNotEmpty(srtingData, "data expected");
            Assert.IsNotNull(intData);
            Assert.IsNotNull(booleanData);
            Assert.IsNotNull(doubleData);
            Assert.IsNotNull(cancellationToken);
            return this.outputData;
        }
    }

    private class MockPipelineProcessException
    {
        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string data)
        {
            ++NumberOfRunInvoced;
            throw new InvalidOperationException("Test exception during process run.");
        }
    }

    [TestMethod]
    public void SuccessfullStepRunWithSingleInput()
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
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

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
    public void SuccessfullStepRunWithArrayInput()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "step_01",
                Take = "data",
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
        var stepStepResult01 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some data from step 01" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessArrayInput(processData);

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

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
    public void SuccessfullStepRunWithManyDifferentInputDataInput()
    {
        var inputConfigs = new List<InputConfig>
        {
            new InputConfig
            {
                From = "step_01",
                Take = "srtingData",
                As = "srtingData",
            },
            new InputConfig
            {
                From = "step_01",
                Take = "intData",
                As = "intData",
            },
            new InputConfig
            {
                From = "step_01",
                Take = "booleanData",
                As = "booleanData",
            },
            new InputConfig
            {
                From = "step_01",
                Take = "doubleData",
                As = "doubleData",
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
        var stepStepResult01 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "srtingData", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some data from step 01" } },
                { "intData", new StepOutput { Action = new HashSet<OutputAction>(), Data = 123456 } },
                { "booleanData", new StepOutput { Action = new HashSet<OutputAction>(), Data = false } },
                { "doubleData", new StepOutput { Action = new HashSet<OutputAction>(), Data = 123.456 } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessManyDifferentInputTypesInput(processData);

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

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

        var processMock = new MockPipelineProcessSingleInput(new Dictionary<string, object>());

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

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

        var processMock = new MockPipelineProcessSingleInput(new Dictionary<string, object>());

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

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

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

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
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = new PipelineStep("my_step", new Dictionary<string, string>() { { "de", "my step" } }, inputConfigs, outputConfigs, processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.IsEmpty(stepResult.Outputs);

        Assert.AreEqual(StepState.Failed, pipelineStep.State);

        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }
}
