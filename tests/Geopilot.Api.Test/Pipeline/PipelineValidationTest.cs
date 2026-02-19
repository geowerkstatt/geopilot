using Geopilot.Api.Pipeline;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineValidationTest
{
    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("noProcesses", new string[] { "PipelineProcessConfig: The Processes field is required.", "StepConfig: process reference for 'xtf_validator'" }, DisplayName = "No Processes")]
    [DataRow("noPipelines", new string[] { "PipelineProcessConfig: The Pipelines field is required." }, DisplayName = "No Pipelines")]
    [DataRow("noStepProcess", new string[] { "StepConfig: The ProcessId field is required.", "StepConfig: process reference for ''" }, DisplayName = "No Process in Step defined")]
    [DataRow("noStepId", new string[] { "StepConfig: The Id field is required." }, DisplayName = "Step has no Id")]
    [DataRow("noStepOutput", new string[] { "StepConfig: The Output field is required." }, DisplayName = "Step has no output defined")]
    [DataRow("noStepInputConfigFrom", new string[] { "InputConfig: The From field is required.", "InputConfig: illegal input from reference from: '', take: 'ili_file' in step 'validation'" }, DisplayName = "Step has no input config 'from'")]
    [DataRow("noStepInputConfigTake", new string[] { "InputConfig: The Take field is required.", "InputConfig: illegal input from reference from: 'upload', take: '' in step 'validation'" }, DisplayName = "Step has no input config 'take'")]
    [DataRow("noStepInputConfigAs", new string[] { "InputConfig: The As field is required." }, DisplayName = "Step has no input config 'as'")]
    [DataRow("noStepOutputConfigTake", new string[] { "OutputConfig: The Take field is required." }, DisplayName = "Step has no output config 'take'")]
    [DataRow("noStepOutputConfigAs", new string[] { "OutputConfig: The As field is required." }, DisplayName = "Step has no output config 'as'")]
    [DataRow("noProcessId", new string[] { "ProcessConfig: The Id field is required.", "StepConfig: process reference for 'xtf_validator'" }, DisplayName = "Process has no Id")]
    [DataRow("noProcessImplementation", new string[] { "ProcessConfig: The Implementation field is required." }, DisplayName = "Process has no Implementation")]
    [DataRow("noPipelineId", new string[] { "PipelineConfig: The Id field is required." }, DisplayName = "Pipeline has no Id")]
    [DataRow("noPipelineParameters", new string[] { "PipelineConfig: The Parameters field is required.", "InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'" }, DisplayName = "Pipeline has no Parameters")]
    [DataRow("noPipelineSteps", new string[] { "PipelineConfig: The Steps field is required." }, DisplayName = "Pipeline has no Steps")]
    [DataRow("noPipelineUploadStep", new string[] { "PipelineParametersConfig: The UploadStep field is required.", "InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'" }, DisplayName = "Pipeline has no Upload Step")]
    [DataRow("noPipelineFileMapping", new string[] { "PipelineParametersConfig: The Mappings field is required.", "InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'" }, DisplayName = "Pipeline has no File Mapping")]
    [DataRow("noPipelineFileMappingExtension", new string[] { "FileMappingsConfig: The FileExtension field is required." }, DisplayName = "Pipeline has no File Mapping Extension")]
    [DataRow("noPipelineFileMappingAttribute", new string[] { "FileMappingsConfig: The Attribute field is required.", "InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'" }, DisplayName = "Pipeline has no File Mapping Attribute")]
    [DataRow("stepWithInvalidProcessReference", new string[] { "StepConfig: process reference for 'invalid_reference'" }, DisplayName = "Step has invalid process reference")]
    [DataRow("unknownProcessImplementation", new string[] { "ProcessConfig: unknown implementation 'this.is.unknown.ProcessorClass' for process 'xtf_validator'" }, DisplayName = "Process has unknown implementation")]
    [DataRow("pipelineNotUnique", new string[] { "PipelineProcessConfig: duplicate pipeline ids found: ili_validation" }, DisplayName = "Pipeline has duplicate ids")]
    [DataRow("processNotUnique", new string[] { "PipelineProcessConfig: duplicate process ids found: xtf_validator" }, DisplayName = "Process has duplicate ids")]
    [DataRow("stepNotUnique", new string[] { "PipelineProcessConfig: duplicate step ids found: not_unique" }, DisplayName = "Step has duplicate ids")]
    [DataRow("invalidStepInputFromReference_01", new string[] { "InputConfig: illegal input from reference from: 'zip', take: 'zip_package' in step 'validation'" }, DisplayName = "Step has invalid input 'from' reference (invalid process reference)")]
    [DataRow("invalidStepInputFromReference_02", new string[] { "InputConfig: illegal input from reference from: 'invalidUploadStep', take: 'ili_file' in step 'validation'" }, DisplayName = "Step has invalid input 'from' reference (invalid upload reference)")]
    [DataRow("notUniqueOutputAs", new string[] { "OutputConfig: not unique output as: 'error' in step 'validation'" }, DisplayName = "Step has not unique output 'as' reference")]
    [DataRow("invalidFileExtension", new string[] { "FileMappingsConfig: invalid file extension '.xtf' in step 'ili_validation'" }, DisplayName = "Step has invalid file extension")]
    public void PipelineValidation(string pipelineFile, string[] expectedErrorMessages)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.IsTrue(validationErrors.HasErrors, "expected validation errors but none found");
        Assert.AreEqual(string.Join(Environment.NewLine, expectedErrorMessages), validationErrors.ErrorMessage);
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        return PipelineFactory
            .Builder()
            .File(path)
            .Configuration(new Mock<IConfiguration>().Object)
            .Build();
    }
}
