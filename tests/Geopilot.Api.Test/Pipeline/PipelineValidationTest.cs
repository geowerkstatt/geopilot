using Geopilot.Api.Pipeline;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineValidationTest
{
    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("noProcesses", "PipelineProcessConfig: The Processes field is required., StepConfig: process reference for 'ili_validator'")]
    [DataRow("noPipelines", "PipelineProcessConfig: The Pipelines field is required.")]
    [DataRow("noStepProcess", "StepConfig: The ProcessId field is required., StepConfig: process reference for ''")]
    [DataRow("noStepId", "StepConfig: The Id field is required.")]
    [DataRow("noStepOutput", "StepConfig: The Output field is required.")]
    [DataRow("noStepInputConfigFrom", "InputConfig: The From field is required., InputConfig: illegal input from reference from: '', take: 'ili_file' in step 'validation'")]
    [DataRow("noStepInputConfigTake", "InputConfig: The Take field is required., InputConfig: illegal input from reference from: 'upload', take: '' in step 'validation'")]
    [DataRow("noStepInputConfigAs", "InputConfig: The As field is required., InputConfig: illegal input as: '' in step 'validation'")]
    [DataRow("noStepOutputConfigTake", "OutputConfig: The Take field is required., OutputConfig: illegal output take: '' in step 'validation'")]
    [DataRow("noStepOutputConfigAs", "OutputConfig: The As field is required.")]
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
    [DataRow("invalidStepInputFromReference_01", "InputConfig: illegal input from reference from: 'zip', take: 'zip_package' in step 'validation'")]
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

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        return PipelineFactory.PipelineFactoryBuilder
            .Builder()
            .File(path)
            .Configuration(new Mock<IConfiguration>().Object)
            .Build();
    }
}
