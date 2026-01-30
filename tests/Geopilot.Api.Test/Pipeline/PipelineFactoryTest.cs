using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Reflection;
using YamlDotNet.Core;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineFactoryTest
{
    [TestMethod(DisplayName = "YAML Validation")]
    [DataRow("noProcesses", "PipelineProcessConfig: The Processes field is required.")]
    [DataRow("noPipelines", "PipelineProcessConfig: The Pipelines field is required.")]
    [DataRow("noStepProcess", "StepConfig: The Process field is required.")]
    [DataRow("noStepName", "StepConfig: The Name field is required.")]
    [DataRow("noStepInput", "StepConfig: The Input field is required.")]
    [DataRow("noStepOutput", "StepConfig: The Output field is required.")]
    [DataRow("noStepInputConfigFrom", "InputConfig: The From field is required.")]
    [DataRow("noStepInputConfigTake", "InputConfig: The Take field is required.")]
    [DataRow("noStepInputConfigAs", "InputConfig: The As field is required.")]
    [DataRow("noStepOutputConfigTake", "OutputConfig: The Take field is required.")]
    [DataRow("noStepOutputConfigAs", "OutputConfig: The As field is required.")]
    [DataRow("noStepOutputConfigAction", "OutputConfig: The Action field is required.")]
    [DataRow("noProcessName", "ProcessConfig: The Name field is required.")]
    [DataRow("noProcessImplementation", "ProcessConfig: The Implementation field is required.")]
    [DataRow("noProcessDataHandling", "ProcessConfig: The DataHandlingConfig field is required.")]
    [DataRow("noPipelineName", "PipelineConfig: The Name field is required.")]
    [DataRow("noPipelineParameters", "PipelineConfig: The Parameters field is required.")]
    [DataRow("noPipelineSteps", "PipelineConfig: The Steps field is required.")]
    [DataRow("noPipelineUploadStep", "PipelineParametersConfig: The UploadStep field is required.")]
    [DataRow("noPipelineFileMapping", "PipelineParametersConfig: The Mapping field is required.")]
    [DataRow("noPipelineFileMappingExtension", "FileMappingConfig: The FileExtension field is required.")]
    [DataRow("noPipelineFileMappingAttribute", "FileMappingConfig: The Attribute field is required.")]
    public void YamlValidation(string pipelineFile, string expectedExceptionMessage)
    {
        YamlException exception = Assert.Throws<YamlException>(() => CreatePipelineFactory(pipelineFile));
        Assert.AreEqual(expectedExceptionMessage, exception.Message);
    }

    [TestMethod]
    public void PipelineNotDefined()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("foo"));
        Assert.AreEqual("pipeline for 'foo' not found", exception.Message);
    }

    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("stepWithInvalidProcessReference", "StepConfig: process reference for 'invalid_reference'")]
    [DataRow("unknownProcessImplementation", "ProcessConfig: unknown implementation 'this.is.unknown.ProcessorClass' for process 'ili_validator'")]
    [DataRow("pipelineNotUnique", "PipelineProcessConfig: duplicate pipeline names found: ili_validation")]
    [DataRow("processNotUnique", "PipelineProcessConfig: duplicate process names found: ili_validator")]
    public void PipelineStepInvalidProcessReference(string pipelineFile, string expectedErrorMessage)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.AreEqual(expectedErrorMessage, validationErrors.ErrorMessage);
    }

    [TestMethod]
    public void CreateBasicPipeline()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        var pipeline = factory.CreatePipeline("ili_validation");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.AreEqual("ili_validation", pipeline.Name, "pipeline name not as expected");
        Assert.IsNotNull(pipeline.Parameters, "pipeline parameters not initialized");
        Assert.AreEqual("upload", pipeline.Parameters.UploadStep, "upload step not as expected");
        Assert.HasCount(1, pipeline.Parameters.Mapping);
        var mapping_0 = pipeline.Parameters.Mapping.ElementAt(0);
        Assert.AreEqual("xtf", mapping_0.FileExtension, "pipeline mapping 0 file extension not as expected");
        Assert.AreEqual("ili_file", mapping_0.Attribute, "pipeline mapping 0 attribute not as expected");
        Assert.HasCount(1, pipeline.Steps);
        var validationStep = pipeline.Steps[0];
        Assert.AreEqual("validation", validationStep.Name, "validation step name not as expected");
        Assert.HasCount(1, validationStep.InputConfig);
        var inputConfig_0 = validationStep.InputConfig.ElementAt(0);
        InputConfig expectedInputConfig_0 = new InputConfig()
        {
            From = "upload",
            Take = "ili_file",
            As = "ili_file",
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
