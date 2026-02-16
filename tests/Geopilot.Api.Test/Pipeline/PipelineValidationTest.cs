using Geopilot.Api.Pipeline;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineValidationTest
{
    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("noProcesses", "PipelineProcessConfig: The Processes field is required., StepConfig: process reference for 'xtf_validator'", DisplayName = "No Processes")]
    [DataRow("noPipelines", "PipelineProcessConfig: The Pipelines field is required.", DisplayName = "No Pipelines")]
    [DataRow("noStepProcess", "StepConfig: The ProcessId field is required., StepConfig: process reference for ''", DisplayName = "No Process in Step defined")]
    [DataRow("noStepId", "StepConfig: The Id field is required.", DisplayName = "Step has no Id")]
    [DataRow("noStepOutput", "StepConfig: The Output field is required.", DisplayName = "Step has no output defined")]
    [DataRow("noStepInputConfigFrom", "InputConfig: The From field is required., InputConfig: illegal input from reference from: '', take: 'ili_file' in step 'validation'", DisplayName = "Step has no input config 'from'")]
    [DataRow("noStepInputConfigTake", "InputConfig: The Take field is required., InputConfig: illegal input from reference from: 'upload', take: '' in step 'validation'", DisplayName = "Step has no input config 'take'")]
    [DataRow("noStepInputConfigAs", "InputConfig: The As field is required.", DisplayName = "Step has no input config 'as'")]
    [DataRow("noStepOutputConfigTake", "OutputConfig: The Take field is required.", DisplayName = "Step has no output config 'take'")]
    [DataRow("noStepOutputConfigAs", "OutputConfig: The As field is required.", DisplayName = "Step has no output config 'as'")]
    [DataRow("noProcessId", "ProcessConfig: The Id field is required., StepConfig: process reference for 'xtf_validator'", DisplayName = "Process has no Id")]
    [DataRow("noProcessImplementation", "ProcessConfig: The Implementation field is required.", DisplayName = "Process has no Implementation")]
    [DataRow("noPipelineId", "PipelineConfig: The Id field is required.", DisplayName = "Pipeline has no Id")]
    [DataRow("noPipelineParameters", "PipelineConfig: The Parameters field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'", DisplayName = "Pipeline has no Parameters")]
    [DataRow("noPipelineSteps", "PipelineConfig: The Steps field is required.", DisplayName = "Pipeline has no Steps")]
    [DataRow("noPipelineUploadStep", "PipelineParametersConfig: The UploadStep field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'", DisplayName = "Pipeline has no Upload Step")]
    [DataRow("noPipelineFileMapping", "PipelineParametersConfig: The Mappings field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'", DisplayName = "Pipeline has no File Mapping")]
    [DataRow("noPipelineFileMappingExtension", "FileMappingsConfig: The FileExtension field is required.", DisplayName = "Pipeline has no File Mapping Extension")]
    [DataRow("noPipelineFileMappingAttribute", "FileMappingsConfig: The Attribute field is required., InputConfig: illegal input from reference from: 'upload', take: 'ili_file' in step 'validation'", DisplayName = "Pipeline has no File Mapping Attribute")]
    [DataRow("stepWithInvalidProcessReference", "StepConfig: process reference for 'invalid_reference'", DisplayName = "Step has invalid process reference")]
    [DataRow("unknownProcessImplementation", "ProcessConfig: unknown implementation 'this.is.unknown.ProcessorClass' for process 'xtf_validator'", DisplayName = "Process has unknown implementation")]
    [DataRow("pipelineNotUnique", "PipelineProcessConfig: duplicate pipeline ids found: ili_validation", DisplayName = "Pipeline has duplicate ids")]
    [DataRow("processNotUnique", "PipelineProcessConfig: duplicate process ids found: xtf_validator", DisplayName = "Process has duplicate ids")]
    [DataRow("stepNotUnique", "PipelineProcessConfig: duplicate step ids found: not_unique", DisplayName = "Step has duplicate ids")]
    [DataRow("invalidStepInputFromReference_01", "InputConfig: illegal input from reference from: 'zip', take: 'zip_package' in step 'validation'", DisplayName = "Step has invalid input 'from' reference (invalid process reference)")]
    [DataRow("invalidStepInputFromReference_02", "InputConfig: illegal input from reference from: 'invalidUploadStep', take: 'ili_file' in step 'validation'", DisplayName = "Step has invalid input 'from' reference (invalid upload reference)")]
    [DataRow("notUniqueOutputAs", "OutputConfig: not unique output as: 'error' in step 'validation'", DisplayName = "Step has not unique output 'as' reference")]
    [DataRow("invalidFileExtension", "FileMappingsConfig: invalid file extension '.xtf' in step 'ili_validation'", DisplayName = "Step has invalid file extension")]
    public void PipelineValidation(string pipelineFile, string expectedErrorMessage)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.IsTrue(validationErrors.HasErrors, "expected validation errors but none found");
        Assert.AreEqual(expectedErrorMessage, validationErrors.ErrorMessage);
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
