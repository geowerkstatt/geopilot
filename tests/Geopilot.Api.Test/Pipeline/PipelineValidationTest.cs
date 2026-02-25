using Geopilot.Api.Pipeline;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineValidationTest
{
    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("noProcesses", new string[] { "PipelineProcessConfig (Processes): Processes are required." }, DisplayName = "No Processes")]
    [DataRow("noPipelines", new string[] { "PipelineProcessConfig (Pipelines): Pipelines are required." }, DisplayName = "No Pipelines")]
    [DataRow("noStepProcess", new string[] { "StepConfig (ProcessId): Process Reference is required." }, DisplayName = "No Process in Step defined")]
    [DataRow("noStepId", new string[] { "StepConfig (Id): Step ID is required." }, DisplayName = "Step has no Id")]
    [DataRow("noStepOutput", new string[] { "StepConfig (Output): Step Output is required." }, DisplayName = "Step has no output defined")]
    [DataRow("noStepInputConfigFrom", new string[] { "PipelineConfig: Step 'validation' has an input reference to '' with attribute 'ili_file' that cannot be found in previous steps or pipeline parameters.", "InputConfig (From): Input from is required." }, DisplayName = "Step has no input config 'from'")]
    [DataRow("noStepInputConfigTake", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'upload' with attribute '' that cannot be found in previous steps or pipeline parameters.", "InputConfig (Take): Input take is required." }, DisplayName = "Step has no input config 'take'")]
    [DataRow("noStepInputConfigAs", new string[] { "InputConfig (As): Input as is required." }, DisplayName = "Step has no input config 'as'")]
    [DataRow("noStepOutputConfigTake", new string[] { "OutputConfig (Take): Output take is required." }, DisplayName = "Step has no output config 'take'")]
    [DataRow("noStepOutputConfigAs", new string[] { "OutputConfig (As): Output as is required." }, DisplayName = "Step has no output config 'as'")]
    [DataRow("noProcessId", new string[] { "PipelineProcessConfig: One or more steps reference a process that is not defined in the processes collection: xtf_validator.", "ProcessConfig (Id): Process ID is required." }, DisplayName = "Process has no Id")]
    [DataRow("noProcessImplementation", new string[] { "ProcessConfig (Implementation): Process Implementation is required." }, DisplayName = "Process has no Implementation")]
    [DataRow("noPipelineId", new string[] { "PipelineConfig (Id): Pipeline ID is required." }, DisplayName = "Pipeline has no Id")]
    [DataRow("noPipelineParameters", new string[] { "PipelineConfig (Parameters): Pipeline parameters are required.", }, DisplayName = "Pipeline has no Parameters")]
    [DataRow("noPipelineSteps", new string[] { "PipelineConfig (Steps): Pipeline Step is required." }, DisplayName = "Pipeline has no Steps")]
    [DataRow("noPipelineUploadStep", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'upload' with attribute 'ili_file' that cannot be found in previous steps or pipeline parameters.", "PipelineParametersConfig (UploadStep): Pipeline Parameter Upload Step is required." }, DisplayName = "Pipeline has no Upload Step")]
    [DataRow("noPipelineFileMapping", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'upload' with attribute 'ili_file' that cannot be found in previous steps or pipeline parameters.", "PipelineParametersConfig (Mappings): Pipeline Parameter Mappings is required." }, DisplayName = "Pipeline has no File Mapping")]
    [DataRow("noPipelineFileMappingExtension", new string[] { "FileMappingsConfig (FileExtension): Pipeline Parameter File Extension is required." }, DisplayName = "Pipeline has no File Mapping Extension")]
    [DataRow("noPipelineFileMappingAttribute", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'upload' with attribute 'ili_file' that cannot be found in previous steps or pipeline parameters.", "FileMappingsConfig (Attribute): Pipeline Parameter File Attribute is required." }, DisplayName = "Pipeline has no File Mapping Attribute")]
    [DataRow("stepWithInvalidProcessReference", new string[] { "PipelineProcessConfig: One or more steps reference a process that is not defined in the processes collection: invalid_reference." }, DisplayName = "Step has invalid process reference")]
    [DataRow("pipelineNotUnique", new string[] { "PipelineProcessConfig (Pipelines): Duplicate Id found: ili_validation." }, DisplayName = "Pipeline has duplicate ids")]
    [DataRow("processNotUnique", new string[] { "PipelineProcessConfig (Processes): Duplicate Id found: xtf_validator." }, DisplayName = "Process has duplicate ids")]
    [DataRow("stepNotUnique", new string[] { "PipelineConfig (Steps): Duplicate Id found: not_unique." }, DisplayName = "Step has duplicate ids")]
    [DataRow("invalidStepInputFromReference_01", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'zip' with attribute 'zip_package' that cannot be found in previous steps or pipeline parameters." }, DisplayName = "Step has invalid input 'from' reference (invalid process reference)")]
    [DataRow("invalidStepInputFromReference_02", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'invalidUploadStep' with attribute 'ili_file' that cannot be found in previous steps or pipeline parameters." }, DisplayName = "Step has invalid input 'from' reference (invalid upload reference)")]
    [DataRow("invalidStepInputTakeReference", new string[] { "PipelineConfig: Step 'zip_package' has an input reference to 'validation' with attribute 'invalid_reference' that cannot be found in previous steps or pipeline parameters." }, DisplayName = "Step has invalid input 'take' reference")]
    [DataRow("notUniqueOutputAs", new string[] { "StepConfig (Output): Duplicate As found: error." }, DisplayName = "Step has not unique output 'as' reference")]
    [DataRow("invalidFileExtension", new string[] { "FileMappingsConfig (FileExtension): invalid file extension" }, DisplayName = "Step has invalid file extension")]
    public void PipelineValidation(string pipelineFile, string[] expectedErrorMessages)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.IsTrue(validationErrors.HasErrors, "expected validation errors but none found");
        var expectedErrorMessage = string.Join(Environment.NewLine, expectedErrorMessages);
        var actualErrorMessage = validationErrors.ErrorMessage;
        Assert.AreEqual(expectedErrorMessage, actualErrorMessage);
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
