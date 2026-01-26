using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineFactoryTest
{
    [TestMethod]
    public void PipelineWithNoProcesses()
    {
        PipelineFactory factory = CreatePipelineFactory("pipelineNoProcesses");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("no processes defined", exception.Message);
    }

    [TestMethod]
    public void PipelineWithNoPipelines()
    {
        PipelineFactory factory = CreatePipelineFactory("pipelineNoPipelines");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("no pipelines defined", exception.Message);
    }

    [TestMethod]
    public void PipelineNotDefined()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("foo"));
        Assert.AreEqual("pipeline for 'foo' not found", exception.Message);
    }

    [TestMethod]
    public void PipelineStepNoProcessDefined()
    {
        PipelineFactory factory = CreatePipelineFactory("stepWithNoProcess");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("no process defined in step", exception.Message);
    }

    [TestMethod]
    public void PipelineStepInvalidProcessReference()
    {
        PipelineFactory factory = CreatePipelineFactory("stepWithInvalidProcessReference");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("process type for 'invalid_reference' not found", exception.Message);
    }

    [TestMethod]
    public void UnknownProcessImplementation()
    {
        PipelineFactory factory = CreatePipelineFactory("unknownProcessImplementation");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("unknown implementation 'this.is.unknown.ProcessorClass' for process 'ili_validator'", exception.Message);
    }

    [TestMethod]
    public void PipelinesNotUnique()
    {
        PipelineFactory factory = CreatePipelineFactory("pipelineNotUnique");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("duplicate pipeline names found: ili_validation", exception.Message);
    }

    [TestMethod]
    public void ProcessNotUnique()
    {
        PipelineFactory factory = CreatePipelineFactory("processNotUnique");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("ili_validation"));
        Assert.AreEqual("duplicate process names found: ili_validator", exception.Message);
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
            WithKey = "file",
        };
        AssertInputConfig(expectedInputConfig_0, inputConfig_0);
        Assert.HasCount(2, validationStep.OutputConfig);
        var outputConfig_0 = validationStep.OutputConfig.ElementAt(0);
        var outputConfig_1 = validationStep.OutputConfig.ElementAt(1);
        OutputConfig expectedOutputConfig_0 = new OutputConfig()
        {
            Take = "error_log",
            As = "error_log",
            WithKey = "error_log",
            Action = OutputAction.IGNORE,
        };
        OutputConfig expectedOutputConfig_1 = new OutputConfig()
        {
            Take = "xtf_log",
            As = "xtf_log",
            WithKey = "xtf_log",
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
        };
        CollectionAssert.AreEqual(expectedDefaultConfig, stepProcess.Config, "process config not as expected");
        Assert.IsNotNull(stepProcess as IliValidatorProcess, "process is not of type ILI Validator");
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Config/Pipeline/" + filename + ".yaml");
        return PipelineFactory.FromFile(path);
    }

    private static void AssertOutputConfig(OutputConfig expectedConfig, OutputConfig actualConfig)
    {
        if (expectedConfig != null && actualConfig != null)
        {
            Assert.AreEqual(expectedConfig.Take, actualConfig.Take, "Output config 'Take' not as expected");
            Assert.AreEqual(expectedConfig.As, actualConfig.As, "Output config 'As' not as expected");
            Assert.AreEqual(expectedConfig.WithKey, actualConfig.WithKey, "Output config 'WithKey' not as expected");
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
            Assert.AreEqual(expectedConfig.WithKey, actualConfig.WithKey, "Output config 'WithKey' not as expected");
        }
        else if (expectedConfig != null && actualConfig == null)
        {
            Assert.Fail("Expected InputConfig is defined bug actual InputConfig is not defined");
        }
        else if (expectedConfig == null && actualConfig != null)
        {
            Assert.Fail("Expected InputConfig is not defined bug actual InputConfig is defined");
        }
    }
}
