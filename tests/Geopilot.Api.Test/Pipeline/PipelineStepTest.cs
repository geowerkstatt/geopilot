using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline.Process;

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

        public int NumberOfRunInvokations { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string[] stringData, int intData, bool booleanData, double doubleData, CancellationToken cancellationToken)
        {
            NumberOfRunInvokations++;
            Assert.IsNotNull(stringData);
            Assert.IsNotEmpty(stringData, "data expected");
            Assert.IsNotNull(intData);
            Assert.IsNotNull(booleanData);
            Assert.IsNotNull(doubleData);
            Assert.IsNotNull(cancellationToken);
            return this.outputData;
        }
    }

    private class MockPipelineProcessNullableTypesInput
    {
        public int NumberOfRunInvokations { get; set; }

        public int NonNullableInt { get; set; }

        public int? NullableInt { get; set; }

        public string NonNullableString { get; set; }

        public string? NullableString { get; set; }

        public int[] ArrayOfNonNullableInts { get; set; }

        public int?[] ArrayOfNullableInts { get; set; }

        public string[] ArrayOfNonNullableStrings { get; set; }

        public string?[] ArrayOfNullableStrings { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(
            int nonNullableInt,
            int? nullableInt,
            string nonNullableString,
            string? nullableString,
            int[] arrayOfNonNullableInts,
            int?[] arrayOfNullableInts,
            string[] arrayOfNonNullableStrings,
            string?[] arrayOfNullableStrings)
        {
            NonNullableInt = nonNullableInt;
            NullableInt = nullableInt;
            NonNullableString = nonNullableString;
            NullableString = nullableString;
            ArrayOfNonNullableInts = arrayOfNonNullableInts;
            ArrayOfNullableInts = arrayOfNullableInts;
            ArrayOfNonNullableStrings = arrayOfNonNullableStrings;
            ArrayOfNullableStrings = arrayOfNullableStrings;
            NumberOfRunInvokations++;
            return [];
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
            NewInputConfig("upload", "xtf_file", "data"),
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
            NewInputConfig("step_01", "data", "data"),
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
            NewInputConfig("step_01", "stringData", "stringData"),
            NewInputConfig("step_01", "intData", "intData"),
            NewInputConfig("step_01", "booleanData", "booleanData"),
            NewInputConfig("step_01", "doubleData", "doubleData"),
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
                { "stringData", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some data from step 01" } },
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

        Assert.AreEqual(1, processMock.NumberOfRunInvokations, "Process Run method was not invoked exactly once.");

        // Assert that the returned StepResult contains the correct content
        Assert.HasCount(1, stepResult.Outputs);
        Assert.IsTrue(stepResult.Outputs.ContainsKey("my_output"));
        Assert.IsEmpty(stepResult.Outputs["my_output"].Action);
        Assert.AreEqual("some_data", stepResult.Outputs["my_output"].Data);
    }

    [TestMethod]
    public void SuccessfullStepRunWithNullableTypesInput()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "int", "nonNullableInt"),
            NewInputConfig("step_01", "null", "nullableInt"),
            NewInputConfig("step_01", "string", "nonNullableString"),
            NewInputConfig("step_01", "null", "nullableString"),
            NewInputConfig("step_01", "int", "arrayOfNonNullableInts"),
            NewInputConfig("step_01", "int", "arrayOfNonNullableInts"),
            NewInputConfig("step_01", "null", "arrayOfNullableInts"),
            NewInputConfig("step_01", "int", "arrayOfNullableInts"),
            NewInputConfig("step_01", "string", "arrayOfNonNullableStrings"),
            NewInputConfig("step_01", "string", "arrayOfNonNullableStrings"),
            NewInputConfig("step_01", "null", "arrayOfNullableStrings"),
            NewInputConfig("step_01", "string", "arrayOfNullableStrings"),
        };
        var stepStepResult01 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "int", new StepOutput { Action = new HashSet<OutputAction>(), Data = 42 } },
                { "string", new StepOutput { Action = new HashSet<OutputAction>(), Data = "this is a string" } },
                { "null", new StepOutput { Action = new HashSet<OutputAction>(), Data = null } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };

        var processMock = new MockPipelineProcessNullableTypesInput();

        using var pipelineStep = new PipelineStep("my_step", [], inputConfigs, [], processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.AreEqual(StepState.Success, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvokations, "Process Run method was not invoked exactly once.");
        Assert.AreEqual(42, processMock.NonNullableInt);
        Assert.IsNull(processMock.NullableInt);
        Assert.AreEqual("this is a string", processMock.NonNullableString);
        Assert.IsNull(processMock.NullableString);
        CollectionAssert.AreEqual(new int[] { 42, 42 }, processMock.ArrayOfNonNullableInts);
        CollectionAssert.AreEqual(new int?[] { null, 42 }, processMock.ArrayOfNullableInts);
        CollectionAssert.AreEqual(new string[] { "this is a string", "this is a string" }, processMock.ArrayOfNonNullableStrings);
        CollectionAssert.AreEqual(new string?[] { null, "this is a string" }, processMock.ArrayOfNullableStrings);
    }

    [TestMethod]
    [DataRow("nonNullableInt")]
    [DataRow("nonNullableString")]
    [DataRow("arrayOfNonNullableInts")]
    [DataRow("arrayOfNonNullableStrings")]
    public void StepRunFailsIfNullValueForNonNullableParameter(string inputToSetNull)
    {
        // Setup input configs with valid values for each parameter
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "int", "nonNullableInt"),
            NewInputConfig("step_01", "null", "nullableInt"),
            NewInputConfig("step_01", "string", "nonNullableString"),
            NewInputConfig("step_01", "null", "nullableString"),
            NewInputConfig("step_01", "int", "arrayOfNonNullableInts"),
            NewInputConfig("step_01", "int", "arrayOfNonNullableInts"),
            NewInputConfig("step_01", "null", "arrayOfNullableInts"),
            NewInputConfig("step_01", "int", "arrayOfNullableInts"),
            NewInputConfig("step_01", "string", "arrayOfNonNullableStrings"),
            NewInputConfig("step_01", "string", "arrayOfNonNullableStrings"),
            NewInputConfig("step_01", "null", "arrayOfNullableStrings"),
            NewInputConfig("step_01", "string", "arrayOfNullableStrings"),
        };

        // Change the specified input parameter to null to test the failure case
        var input = inputConfigs.First(i => i.As == inputToSetNull);
        input.Take = "null";

        var stepStepResult01 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "int", new StepOutput { Action = new HashSet<OutputAction>(), Data = 42 } },
                { "string", new StepOutput { Action = new HashSet<OutputAction>(), Data = "this is a string" } },
                { "null", new StepOutput { Action = new HashSet<OutputAction>(), Data = null } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };

        var processMock = new MockPipelineProcessNullableTypesInput();

        using var pipelineStep = new PipelineStep("my_step", [], inputConfigs, [], processMock);

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.AreEqual(StepState.Failed, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvokations, "Process Run method was invoked.");
    }

    [TestMethod]
    public void StepRunFailsIfInputDataIsOfWrongType()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "stringData", "stringData"),
            NewInputConfig("step_01", "intData", "intData"),
            NewInputConfig("step_01", "not_boolean_data", "booleanData"),
            NewInputConfig("step_01", "doubleData", "doubleData"),
        };
        var stepStepResult01 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "stringData", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some data from step 01" } },
                { "intData", new StepOutput { Action = new HashSet<OutputAction>(), Data = 123456 } },
                { "not_boolean_data", new StepOutput { Action = new HashSet<OutputAction>(), Data = "expected to be a boolean but is a string" } },
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
        var processMock = new MockPipelineProcessManyDifferentInputTypesInput([]);
        using var pipelineStep = new PipelineStep("my_step", [], inputConfigs, [], processMock);
        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.AreEqual(StepState.Failed, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvokations, "Process Run method was invoked.");
    }

    [TestMethod]
    public void StepRunFailsIfInputDataIsOfWrongTypeForArray()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "intData", "intData"),
            NewInputConfig("step_01", "booleanData", "booleanData"),
            NewInputConfig("step_01", "doubleData", "doubleData"),
            NewInputConfig("step_01", "string_data", "stringData"),
            NewInputConfig("step_01", "not_string_data", "stringData"),
        };
        var stepStepResult01 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "not_string_data", new StepOutput { Action = new HashSet<OutputAction>(), Data = 42 } },
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
        var processMock = new MockPipelineProcessManyDifferentInputTypesInput([]);
        using var pipelineStep = new PipelineStep("my_step", [], inputConfigs, [], processMock);
        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = Task.Run(() => pipelineStep.Run(pipelineContext, CancellationToken.None)).GetAwaiter().GetResult();

        Assert.AreEqual(StepState.Failed, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvokations, "Process Run method was invoked.");
    }

    [TestMethod]
    public void ContextHasNoReferencingStepRusult()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("invalid_upload_reference", "xtf_file", "data"),
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

        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked.");
    }

    [TestMethod]
    public void ContextHasNoReferencingStepOutput()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("upload", "xtf_file_wrong_reference", "file_to_validate"),
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
            NewInputConfig("upload", "xtf_file", "data"),
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
            NewInputConfig("upload", "xtf_file", "data"),
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

    private InputConfig NewInputConfig(string from, string take, string asInput)
    {
        return new InputConfig
        {
            From = from,
            Take = take,
            As = asInput,
        };
    }
}
