using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Reflection;
using YamlDotNet.Core;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineFactoryTest
{
    [TestMethod]
    public void PipelineNotDefined()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("foo"));
        Assert.AreEqual("pipeline for 'foo' not found", exception.Message);
    }

    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("noProcesses", "PipelineProcessConfig: The Processes field is required., StepConfig: process reference for 'ili_validator'")]
    [DataRow("noPipelines", "PipelineProcessConfig: The Pipelines field is required.")]
    [DataRow("noStepProcess", "StepConfig: The ProcessId field is required., StepConfig: process reference for ''")]
    [DataRow("noStepId", "StepConfig: The Id field is required.")]
    [DataRow("noStepInput", "StepConfig: The Input field is required.")]
    [DataRow("noStepOutput", "StepConfig: The Output field is required.")]
    [DataRow("noStepInputConfigFrom", "InputConfig: The From field is required., InputConfig: illegal input from reference from: '', take: 'ili_file' in step 'validation'")]
    [DataRow("noStepInputConfigTake", "InputConfig: The Take field is required., InputConfig: illegal input from reference from: 'upload', take: '' in step 'validation'")]
    [DataRow("noStepInputConfigAs", "InputConfig: The As field is required., InputConfig: illegal input as: '' in step 'validation'")]
    [DataRow("noStepOutputConfigTake", "OutputConfig: The Take field is required., OutputConfig: illegal output take: '' in step 'validation'")]
    [DataRow("noStepOutputConfigAs", "OutputConfig: The As field is required.")]
    [DataRow("noStepOutputConfigAction", "OutputConfig: The Action field is required.")]
    [DataRow("noProcessId", "ProcessConfig: The Id field is required., StepConfig: process reference for 'ili_validator'")]
    [DataRow("noProcessImplementation", "ProcessConfig: The Implementation field is required.")]
    [DataRow("noProcessDataHandling", "ProcessConfig: The DataHandlingConfig field is required.")]
    [DataRow("noPipelineId", "PipelineConfig: The Id field is required.")]
    [DataRow("noPipelineParameters", "PipelineConfig: The Parameters field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'")]
    [DataRow("noPipelineSteps", "PipelineConfig: The Steps field is required.")]
    [DataRow("noPipelineUploadStep", "PipelineParametersConfig: The UploadStep field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'")]
    [DataRow("noPipelineFileMapping", "PipelineParametersConfig: The Mappings field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'")]
    [DataRow("noPipelineFileMappingExtension", "FileMappingsConfig: The FileExtension field is required.")]
    [DataRow("noPipelineFileMappingAttribute", "FileMappingsConfig: The Attribute field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'")]
    [DataRow("stepWithInvalidProcessReference", "StepConfig: process reference for 'invalid_reference'")]
    [DataRow("unknownProcessImplementation", "ProcessConfig: unknown implementation 'this.is.unknown.ProcessorClass' for process 'ili_validator'")]
    [DataRow("pipelineNotUnique", "PipelineProcessConfig: duplicate pipeline ids found: ili_validation")]
    [DataRow("processNotUnique", "PipelineProcessConfig: duplicate process ids found: ili_validator")]
    [DataRow("stepNotUnique", "PipelineProcessConfig: duplicate step ids found: not_unique")]
    [DataRow("invalidStepInputFromReference_01", "InputConfig: illegal input from reference from: 'dummy', take: 'error_log' in step 'validation'")]
    [DataRow("invalidStepInputFromReference_02", "InputConfig: illegal input from reference from: 'invalidUploadStep', take: 'ili_file' in step 'validation'")]
    [DataRow("invalidStepInputTakeReference_01", "InputConfig: illegal input from reference from: 'upload', take: 'invalid_take_reference' in step 'validation'")]
    [DataRow("notUniqueOutputAs", "OutputConfig: not unique output as: 'error' in step 'validation'")]
    [DataRow("stepProcessMappingIllegalTake", "OutputConfig: illegal output take: 'error_log' in step 'validation'")]
    [DataRow("invalidFileExtension", "FileMappingsConfig: invalid file extension '.xtf' in step 'ili_validation'")]
    public void PipelineValidation(string pipelineFile, string expectedErrorMessage)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.IsTrue(validationErrors.HasErrors, "expected validation errors but none found");
        Assert.AreEqual(expectedErrorMessage, validationErrors.ErrorMessage);
    }

    [TestMethod]
    public void CreateBasicPipeline()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        var pipeline = factory.CreatePipeline("ili_validation");
        Assert.AreEqual(PipelineState.Pending, pipeline.State, "pipeline state not as expected");
        Assert.AreEqual(StepState.Pending, pipeline.Steps[0].State, "step state not as expected");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.AreEqual("ili_validation", pipeline.Id, "pipeline name not as expected");
        Assert.IsNotNull(pipeline.Parameters, "pipeline parameters not initialized");
        Assert.AreEqual("upload", pipeline.Parameters.UploadStep, "upload step not as expected");
        Assert.HasCount(1, pipeline.Parameters.Mappings);
        var mapping_0 = pipeline.Parameters.Mappings.ElementAt(0);
        Assert.AreEqual("xtf", mapping_0.FileExtension, "pipeline mapping 0 file extension not as expected");
        Assert.AreEqual("ili_file", mapping_0.Attribute, "pipeline mapping 0 attribute not as expected");
        Assert.HasCount(1, pipeline.Steps);
        var validationStep = pipeline.Steps[0];
        Assert.AreEqual("validation", validationStep.Id, "validation step name not as expected");
        Assert.HasCount(1, validationStep.InputConfig);
        var inputConfig_0 = validationStep.InputConfig.ElementAt(0);
        InputConfig expectedInputConfig_0 = new InputConfig()
        {
            From = "upload",
            Take = "ili_file",
            As = "file",
        };
        AssertInputConfig(expectedInputConfig_0, inputConfig_0);
        Assert.HasCount(2, validationStep.OutputConfigs);
        var outputConfig_0 = validationStep.OutputConfigs.ElementAt(0);
        var outputConfig_1 = validationStep.OutputConfigs.ElementAt(1);
        OutputConfig expectedOutputConfig_0 = new OutputConfig()
        {
            Take = "error_log",
            As = "error_log",
            Action = OutputAction.IGNORE,
        };
        OutputConfig expectedOutputConfig_1 = new OutputConfig()
        {
            Take = "xtf_log",
            As = "xtf_log",
            Action = OutputAction.DOWNLOAD,
        };
        AssertOutputConfig(expectedOutputConfig_0, outputConfig_0);
        AssertOutputConfig(expectedOutputConfig_1, outputConfig_1);
        IPipelineProcess stepProcess = validationStep.Process;
        Assert.IsNotNull(stepProcess, "step process not created");
        Assert.AreEqual("ili_validator", stepProcess.Name, "process name not as expected");
        var expectedDataHandlingInputMappingConfig = new Dictionary<string, string>() { { "ili_file", "file" }, };
        var expectedDataHandlingOutputMappingConfig = new Dictionary<string, string>() { { "error_log", "error_log" }, { "xtf_log", "xtf_log" }, };
        Assert.IsNotNull(stepProcess.DataHandlingConfig, "step process data handling config not defined");
        CollectionAssert.AreEqual(expectedDataHandlingInputMappingConfig, stepProcess.DataHandlingConfig.InputMapping, "process data handling input mapping config not as expected");
        CollectionAssert.AreEqual(expectedDataHandlingOutputMappingConfig, stepProcess.DataHandlingConfig.OutputMapping, "process data handling output mapping config not as expected");
        var expectedDefaultConfig = new Dictionary<string, string>()
        {
            { "log_level", "DEBUG" },
            { "profile", "PROFILE-A" },
        };
        CollectionAssert.AreEqual(expectedDefaultConfig, stepProcess.Config, "process config not as expected");
        Assert.IsNotNull(stepProcess as IliValidatorProcess, "process is not of type ILI Validator");
    }

    [TestMethod]
    public void CreateBasicPipelineNoProcessConfigOverwrite()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipelineNoProcessConfigOverwrite");
        var pipeline = factory.CreatePipeline("ili_validation");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(1, pipeline.Steps);
        var validationStep = pipeline.Steps[0];
        IPipelineProcess stepProcess = validationStep.Process;
        Assert.IsNotNull(stepProcess, "step process not created");
        var expectedDefaultConfig = new Dictionary<string, string>();
        CollectionAssert.AreEqual(expectedDefaultConfig, stepProcess.Config, "process config not as expected");
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        return PipelineFactory.FromFile(path);
    }

    private static void AssertOutputConfig(OutputConfig expectedConfig, OutputConfig actualConfig)
    {
        if (expectedConfig != null && actualConfig != null)
        {
            Assert.AreEqual(expectedConfig.Take, actualConfig.Take, "Output config 'Take' not as expected");
            Assert.AreEqual(expectedConfig.As, actualConfig.As, "Output config 'As' not as expected");
            Assert.AreEqual(expectedConfig.Action, actualConfig.Action, "Output config 'Action' not as expected");
        }
        else if (expectedConfig != null && actualConfig == null)
        {
            Assert.Fail("Expected OutputConfig is defined bug actual OutputConfig is not defined");
        }
        else if (expectedConfig == null && actualConfig != null)
        {
            Assert.Fail("Expected OutputConfig is not defined bug actual OutputConfig is defined");
        }
    }

    private static void AssertInputConfig(InputConfig expectedConfig, InputConfig actualConfig)
    {
        if (expectedConfig != null && actualConfig != null)
        {
            Assert.AreEqual(expectedConfig.From, actualConfig.From, "Output config 'From' not as expected");
            Assert.AreEqual(expectedConfig.Take, actualConfig.Take, "Output config 'Take' not as expected");
            Assert.AreEqual(expectedConfig.As, actualConfig.As, "Output config 'As' not as expected");
        }
        else if (expectedConfig != null && actualConfig == null)
        {
            Assert.Fail("Expected InputConfig is defined but actual InputConfig is not defined");
        }
        else if (expectedConfig == null && actualConfig != null)
        {
            Assert.Fail("Expected InputConfig is not defined but actual InputConfig is defined");
        }
    }
}
