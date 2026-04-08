using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    [DataRow("noStepInputConfigFrom", new string[] { "PipelineConfig: Step 'validation' has an input reference to '' with attribute 'ili_file' that cannot be found in previous steps.", "InputConfig (From): Input from is required." }, DisplayName = "Step has no input config 'from'")]
    [DataRow("noStepInputConfigTake", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'upload' with attribute '' that cannot be found in previous steps.", "InputConfig (Take): Input take is required." }, DisplayName = "Step has no input config 'take'")]
    [DataRow("noStepInputConfigAs", new string[] { "InputConfig (As): Input as is required." }, DisplayName = "Step has no input config 'as'")]
    [DataRow("noStepOutputConfigTake", new string[] { "OutputConfig (Take): Output take is required." }, DisplayName = "Step has no output config 'take'")]
    [DataRow("noStepOutputConfigAs", new string[] { "OutputConfig (As): Output as is required." }, DisplayName = "Step has no output config 'as'")]
    [DataRow("noProcessId", new string[] { "PipelineProcessConfig: One or more steps reference a process that is not defined in the processes collection: xtf_validator.", "ProcessConfig (Id): Process ID is required." }, DisplayName = "Process has no Id")]
    [DataRow("noProcessImplementation", new string[] { "ProcessConfig (Implementation): Process Implementation is required." }, DisplayName = "Process has no Implementation")]
    [DataRow("noPipelineId", new string[] { "PipelineConfig (Id): Pipeline ID is required." }, DisplayName = "Pipeline has no Id")]
    [DataRow("noPipelineSteps", new string[] { "PipelineConfig (Steps): Pipeline Step is required." }, DisplayName = "Pipeline has no Steps")]
    [DataRow("stepWithInvalidProcessReference", new string[] { "PipelineProcessConfig: One or more steps reference a process that is not defined in the processes collection: invalid_reference." }, DisplayName = "Step has invalid process reference")]
    [DataRow("pipelineNotUnique", new string[] { "PipelineProcessConfig: Duplicate Id found: ili_validation." }, DisplayName = "Pipeline has duplicate ids")]
    [DataRow("processNotUnique", new string[] { "PipelineProcessConfig: Duplicate Id found: xtf_validator." }, DisplayName = "Process has duplicate ids")]
    [DataRow("stepNotUnique", new string[] { "PipelineConfig: Duplicate Id found: not_unique." }, DisplayName = "Step has duplicate ids")]
    [DataRow("invalidStepInputFromReference_01", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'zip' with attribute 'zip_package' that cannot be found in previous steps." }, DisplayName = "Step has invalid input 'from' reference (invalid process reference)")]
    [DataRow("invalidStepInputFromReference_02", new string[] { "PipelineConfig: Step 'validation' has an input reference to 'invalidUploadStep' with attribute 'ili_file' that cannot be found in previous steps." }, DisplayName = "Step has invalid input 'from' reference (invalid upload reference)")]
    [DataRow("invalidStepInputTakeReference", new string[] { "PipelineConfig: Step 'zip_package' has an input reference to 'validation' with attribute 'invalid_reference' that cannot be found in previous steps." }, DisplayName = "Step has invalid input 'take' reference")]
    [DataRow("notUniqueOutputAs", new string[] { "StepConfig: Duplicate As found: error." }, DisplayName = "Step has not unique output 'as' reference")]
    [DataRow("invalidStepPreSkipCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '[upload.foo] != null' on field Step-Pre-Skip-Condition, parameter 'upload.foo' is not valid" }, DisplayName = "Step pre skip condition is not valid (invalid parameter reference)")]
    [DataRow("invalidStepPreSkipCondition_02", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '([upload.ili_file]' on field Step-Pre-Skip-Condition: Error parsing the expression." }, DisplayName = "Step pre skip condition is not valid (invalid expression)")]
    [DataRow("invalidStepPreSkipCondition_03", new string[] { "PipelineConfig: pipeline 'two_steps', step 'validation', invalid expression '[zip_package_process.archive] != null' on field Step-Pre-Skip-Condition, parameter 'zip_package_process.archive' is not valid" }, DisplayName = "Step pre skip condition is not valid (invalid forward parameter reference)")]
    [DataRow("invalidStepPreFailCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '[upload.foo] != null' on field Step-Pre-Fail-Condition, parameter 'upload.foo' is not valid" }, DisplayName = "Step pre fail condition is not valid (invalid parameter reference)")]
    [DataRow("invalidStepPreFailCondition_02", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '([upload.ili_file]' on field Step-Pre-Fail-Condition: Error parsing the expression." }, DisplayName = "Step pre fail condition is not valid (invalid expression)")]
    [DataRow("invalidStepPreFailCondition_03", new string[] { "PipelineConfig: pipeline 'two_steps', step 'validation', invalid expression '[zip_package_process.archive] != null' on field Step-Pre-Fail-Condition, parameter 'zip_package_process.archive' is not valid" }, DisplayName = "Step pre fail condition is not valid (invalid forward parameter reference)")]
    [DataRow("invalidPipelineDeliveryCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', invalid expression '[upload.foo] != null' on field Pipeline-Delivery-Restriction, parameter 'upload.foo' is not valid" }, DisplayName = "Pipeline delivery condition is not valid (invalid parameter reference)")]
    [DataRow("invalidPipelineDeliveryCondition_02", new string[] { "PipelineConfig: pipeline 'ili_validation, invalid expression '([upload.ili_file]' on field Pipeline-Delivery-Restriction: Error parsing the expression." }, DisplayName = "Pipeline delivery condition is not valid (invalid expression)")]
    [DataRow("overwriteUndefinedBaseConfig", new string[] { "PipelineProcessConfig: Step 'validation' in pipeline 'ili_validation' is trying to overwrite process config parameter 'validationProfile' which is not defined in the default config." }, DisplayName = "overwrite a undefined base config parameter")]

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
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        var fileAccessOptions = new FileAccessOptions()
        {
            UploadDirectory = Path.Combine(Path.GetTempPath(), "Upload"),
            AssetsDirectory = Path.Combine(Path.GetTempPath(), "Asset"),
            PipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline"),
        };
        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(new Mock<IPipelineProcessFactory>().Object)
            .LoggerFactory(loggerFactoryMock.Object)
            .DirectoryProvider(new DirectoryProvider(Options.Create(fileAccessOptions)))
            .Build();
    }
}
