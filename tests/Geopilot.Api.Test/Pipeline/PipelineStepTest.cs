using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineStepTest
{
    private Mock<ILoggerFactory> loggerFactoryMock;
    private Mock<ILogger<PipelineProcessFactory>> loggerMock;

    [TestInitialize]
    public void SetUp()
    {
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
    }

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
        public MockPipelineProcessArrayInput(Dictionary<string, object> outputData, int expectedNumberOfInputData)
        {
            this.outputData = outputData;
            this.expectedNumberOfInputData = expectedNumberOfInputData;
        }

        private Dictionary<string, object> outputData;
        private int expectedNumberOfInputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string[] data, CancellationToken cancellationToken)
        {
            Assert.IsNotNull(data);
            Assert.IsNotEmpty(data, "data expected");
            Assert.HasCount(expectedNumberOfInputData, data, "not matching expected count for input data");
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
    public async Task SuccessfullStepRunWithSingleInput()
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
            Upload = new PipelineFileList(),
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

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task SuccessfullStepRunWithArrayInputFromSingleSources()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "data", "data"),
            NewInputConfig("step_02", "data", "data"),
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
        var stepStepResult02 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = "some data from step 02" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
                { "step_02", stepStepResult02 },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessArrayInput(processData, 2);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task SuccessfullStepRunWithArrayInputFromOneSingleArray()
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
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = new string[] { "data 01 from step 01", "data 02 from step 01" } } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessArrayInput(processData, 2);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task SuccessfullStepRunWithArrayInputFromOneArrayAndOneSigleParameter()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "data", "data"),
            NewInputConfig("step_02", "data", "data"),
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
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = new string[] { "data 01 from step 01", "data 02 from step 01" } } },
            },
        };
        var stepStepResult02 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = "data 01 from step 02" } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
                { "step_02", stepStepResult02 },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessArrayInput(processData, 3);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task SuccessfullStepRunWithArrayInputFromTwoArrays()
    {
        var inputConfigs = new List<InputConfig>
        {
            NewInputConfig("step_01", "data", "data"),
            NewInputConfig("step_02", "data", "data"),
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
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = new string[] { "data 01 from step 01", "data 02 from step 01" } } },
            },
        };
        var stepStepResult02 = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "data", new StepOutput { Action = new HashSet<OutputAction>(), Data = new string[] { "data 01 from step 02", "data 02 from step 02" } } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
                { "step_02", stepStepResult02 },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };

        var processMock = new MockPipelineProcessArrayInput(processData, 4);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task SuccessfullStepRunWithManyDifferentInputDataInput()
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
            Upload = new PipelineFileList(),
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

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task SuccessfullStepRunWithNullableTypesInput()
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };

        var processMock = new MockPipelineProcessNullableTypesInput();

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName([])
            .InputConfig(inputConfigs)
            .OutputConfig([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

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
    public async Task StepRunFailsIfNullValueForNonNullableParameter(string inputToSetNull)
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };

        var processMock = new MockPipelineProcessNullableTypesInput();

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName([])
            .InputConfig(inputConfigs)
            .OutputConfig([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.Contains("non-nullable", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvokations, "Process Run method was invoked.");
    }

    [TestMethod]
    public async Task StepRunFailsIfInputDataIsOfWrongType()
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };
        var processMock = new MockPipelineProcessManyDifferentInputTypesInput([]);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName([])
            .InputConfig(inputConfigs)
            .OutputConfig([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("The mapped input value of type <System.String> was not assignable to parameter <booleanData> of type <System.Boolean>.", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvokations, "Process Run method was invoked.");
    }

    [TestMethod]
    public async Task StepRunFailsIfInputDataIsOfWrongTypeForArray()
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "step_01", stepStepResult01 },
            },
        };
        var processMock = new MockPipelineProcessManyDifferentInputTypesInput([]);
        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName([])
            .InputConfig(inputConfigs)
            .OutputConfig([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();
        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("At least one of the mapped input values was not assignable to the element type <String> of parameter <stringData> of type <System.String[]>.", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvokations, "Process Run method was invoked.");
    }

    [TestMethod]
    public async Task ContextHasNoReferencingStepResult()
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };

        var processMock = new MockPipelineProcessSingleInput(new Dictionary<string, object>());

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("Could not find matching data for parameter <data> of type <System.String> in process run method.", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked.");
    }

    [TestMethod]
    public async Task ContextHasNoReferencingStepOutput()
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };

        var processMock = new MockPipelineProcessSingleInput(new Dictionary<string, object>());

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("Could not find matching data for parameter <data> of type <System.String> in process run method.", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public async Task ExceptionDuringProcessRun()
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
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
            },
        };

        var processMock = new MockPipelineProcessException();

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("The process <MockPipelineProcessException> threw an exception.", exception.Message);
        Assert.AreEqual(typeof(InvalidOperationException), exception.InnerException?.GetType());
        Assert.AreEqual("Test exception during process run.", exception.InnerException?.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public async Task StepResultCouldNotBeCreated()
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
            Upload = new PipelineFileList(),
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

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("Output config is missing 'take' or 'as', or the process output (referenced by 'take') was not found in the output of the process. This error should not occur. Please consolidate the pipeline validation logic.", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public async Task StepShouldFailBecauseOfPreCondition()
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
        var aPreviousStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "some_random_data", new StepOutput { Action = new HashSet<OutputAction>(), Data = 123 } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
                { "aPreviousStep", aPreviousStepResult },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipCondition = "[aPreviousStep.some_random_data] == 123",
                FailCondition = "[aPreviousStep.some_random_data] == 123",
            },
            Post = null,
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.IsNotNull(stepResult);

        Assert.AreEqual(StepState.Error, pipelineStep.State);

        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked but should be skipped.");
    }

    [TestMethod]
    public async Task StepShouldBeSkipedBecauseOfPreCondition()
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
        var aPreviousStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "some_random_data", new StepOutput { Action = new HashSet<OutputAction>(), Data = 123 } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
                { "aPreviousStep", aPreviousStepResult },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipCondition = "[aPreviousStep.some_random_data] == 123",
                FailCondition = null,
            },
            Post = null,
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.IsNotNull(stepResult);

        Assert.AreEqual(StepState.Skipped, pipelineStep.State);

        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked but should be skipped.");
    }

    [TestMethod]
    public async Task StepShouldFailBecauseOfPostCondition()
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
        var aPreviousStepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>
            {
                { "some_random_data", new StepOutput { Action = new HashSet<OutputAction>(), Data = 123 } },
            },
        };
        var pipelineContext = new PipelineContext()
        {
            Upload = new PipelineFileList(),
            StepResults = new Dictionary<string, StepResult>()
            {
                { "upload", uploadStepResult },
                { "aPreviousStep", aPreviousStepResult },
            },
        };
        var processData = new Dictionary<string, object>()
        {
            { "error_log", "some_data" },
        };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipCondition = "[aPreviousStep.some_random_data] == 124",
                FailCondition = "[aPreviousStep.some_random_data] == 124",
            },
            Post = new PipelineStepPostConditionConfig()
            {
                FailCondition = "[my_step.my_output] == 'some_data'",
            },
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .InputConfig(inputConfigs)
            .OutputConfig(outputConfigs)
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.IsNotNull(stepResult);

        Assert.AreEqual(StepState.Error, pipelineStep.State);

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
