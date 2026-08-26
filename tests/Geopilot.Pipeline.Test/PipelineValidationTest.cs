using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineValidationTest
{
    private const string XtfValidatorImplementation = "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess";

    private const string CollisionOrigin =
        "Process configuration collision for implementation '" + XtfValidatorImplementation + "': " +
        "the key 'validationProfile' is set in the base configuration " +
        "(app settings 'Pipeline:ProcessConfigs:" + XtfValidatorImplementation + ":validationProfile', " +
        "environment variable 'Pipeline__ProcessConfigs__" + XtfValidatorImplementation + "__validationProfile')";

    private const string CollisionRemedy =
        " The base configuration cannot be overridden. Remove the key from the pipeline definition, " +
        "or remove it from the base configuration if the value has to be set per pipeline.";

    private const string DefaultConfigCollision =
        CollisionOrigin + " and in the pipeline definition " +
        "('processes[id=xtf_validator].default_config.validationProfile')." + CollisionRemedy;

    private const string StepOverwriteCollision =
        CollisionOrigin + " and overwritten in the pipeline definition " +
        "('pipelines[id=ili_validation].steps[id=validation].process_config_overwrites.validationProfile')." + CollisionRemedy;

    [TestMethod(DisplayName = "Pipeline Validation")]
    [DataRow("noProcesses", new string[] { "PipelineProcessConfig (Processes): Processes are required." }, DisplayName = "No Processes")]
    [DataRow("noPipelines", new string[] { "PipelineProcessConfig (Pipelines): Pipelines are required." }, DisplayName = "No Pipelines")]
    [DataRow("noStepProcess", new string[] { "StepConfig (ProcessId): Process Reference is required." }, DisplayName = "No Process in Step defined")]
    [DataRow("noStepId", new string[] { "StepConfig (Id): Step ID is required." }, DisplayName = "Step has no Id")]
    [DataRow("noStepDisplayName", new string[] { "StepConfig (DisplayName): Step Display Name is required." }, DisplayName = "Step has no Display Name")]
    [DataRow("emptyStepDisplayName", new string[] { "StepConfig (DisplayName): Step Display Name is required." }, DisplayName = "Step has empty Display Name")]
    [DataRow("noProcessId", new string[] { "PipelineProcessConfig: One or more steps reference a process that is not defined in the processes collection: xtf_validator.", "ProcessConfig (Id): Process ID is required." }, DisplayName = "Process has no Id")]
    [DataRow("noProcessImplementation", new string[] { "ProcessConfig (Implementation): Process Implementation is required." }, DisplayName = "Process has no Implementation")]
    [DataRow("noPipelineId", new string[] { "PipelineConfig (Id): Pipeline ID is required." }, DisplayName = "Pipeline has no Id")]
    [DataRow("noPipelineSteps", new string[] { "PipelineConfig (Steps): Pipeline Step is required." }, DisplayName = "Pipeline has no Steps")]
    [DataRow("noPipelineDisplayName", new string[] { "PipelineConfig (DisplayName): Pipeline Display Name is required." }, DisplayName = "Pipeline has no Display Name")]
    [DataRow("emptyPipelineDisplayName", new string[] { "PipelineConfig (DisplayName): Pipeline Display Name is required." }, DisplayName = "Pipeline has empty Display Name")]
    [DataRow("stepWithInvalidProcessReference", new string[] { "PipelineProcessConfig: One or more steps reference a process that is not defined in the processes collection: invalid_reference." }, DisplayName = "Step has invalid process reference")]
    [DataRow("pipelineNotUnique", new string[] { "PipelineProcessConfig: Duplicate Id found: ili_validation." }, DisplayName = "Pipeline has duplicate ids")]
    [DataRow("processNotUnique", new string[] { "PipelineProcessConfig: Duplicate Id found: xtf_validator." }, DisplayName = "Process has duplicate ids")]
    [DataRow("stepNotUnique", new string[] { "PipelineConfig: Duplicate Id found: not_unique." }, DisplayName = "Step has duplicate ids")]
    [DataRow("invalidStepInputFromReference_01", new string[] { "PipelineConfig: Step 'validation' input 'transferFile' references 'zip.zip_package', but 'zip' is not an earlier step." }, DisplayName = "Step input references a later step")]
    [DataRow("invalidStepInputFromReference_02", new string[] { "PipelineConfig: Step 'validation' input 'transferFile' references 'invalidUploadStep.ili_file', but 'invalidUploadStep' is not an earlier step." }, DisplayName = "Step input references an unknown step")]
    [DataRow("invalidStepInputMalformedReference", new string[] { "PipelineConfig: Step 'validation': Reference '${step_output(bad)}' must be of the form ${step_output(stepId.outputName)}." }, DisplayName = "Step input has a malformed reference")]
    [DataRow("invalidStepInputUnsupportedReference", new string[] { "PipelineConfig: Step 'validation': Reference '${uploads}' is not supported. Use ${step_output(stepId.outputName)}, ${file(path)} or ${upload()}." }, DisplayName = "Step input has an unsupported reference")]
    [DataRow("invalidStepInputNestedSequence", new string[] { "PipelineConfig: Step 'validation': Input 'transferFile': a list must not contain another list. Nested lists are not supported." }, DisplayName = "Step input has a nested list")]
    [DataRow("invalidStepInputRootedFilePath", new string[] { "PipelineConfig: Step 'validation': Reference '${file(/templates/header.xtf)}' must be of the form ${file(path)} with a relative path that does not contain '.' or '..' segments." }, DisplayName = "Step input file reference is rooted")]
    [DataRow("notUniqueOutputActionProperty", new string[] { "StepConfig: Duplicate Property found: ErrorLog." }, DisplayName = "Step has duplicate output action property")]
    [DataRow("emptyOutputActions", new string[] { "StepConfig (OutputActions): At least one output action is required." }, DisplayName = "Step has an empty output actions list")]
    [DataRow("invalidStepPreSkipCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '[upload.foo] != null' on field Step-Pre-Skip-Condition, parameter 'upload.foo' is not valid" }, DisplayName = "Step pre skip condition is not valid (invalid parameter reference)")]
    [DataRow("invalidStepPreSkipCondition_02", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '([upload.ili_file]' on field Step-Pre-Skip-Condition: Error parsing the expression." }, DisplayName = "Step pre skip condition is not valid (invalid expression)")]
    [DataRow("invalidStepPreSkipCondition_03", new string[] { "PipelineConfig: pipeline 'two_steps', step 'validation', invalid expression '[zip_package_process.archive] != null' on field Step-Pre-Skip-Condition, parameter 'zip_package_process.archive' is not valid" }, DisplayName = "Step pre skip condition is not valid (invalid forward parameter reference)")]
    [DataRow("invalidStepPreFailCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '[upload.foo] != null' on field Step-Pre-Fail-Condition, parameter 'upload.foo' is not valid" }, DisplayName = "Step pre fail condition is not valid (invalid parameter reference)")]
    [DataRow("invalidStepPreFailCondition_02", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '([upload.ili_file]' on field Step-Pre-Fail-Condition: Error parsing the expression." }, DisplayName = "Step pre fail condition is not valid (invalid expression)")]
    [DataRow("invalidStepPreFailCondition_03", new string[] { "PipelineConfig: pipeline 'two_steps', step 'validation', invalid expression '[zip_package_process.archive] != null' on field Step-Pre-Fail-Condition, parameter 'zip_package_process.archive' is not valid" }, DisplayName = "Step pre fail condition is not valid (invalid forward parameter reference)")]
    [DataRow("invalidStepPostWarnCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '[upload.foo] != null' on field Step-Post-Warn-Condition, parameter 'upload.foo' is not valid" }, DisplayName = "Step post warn condition is not valid (invalid parameter reference)")]
    [DataRow("invalidStepPostRestrictDeliveryCondition_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'validation', invalid expression '[upload.foo] != null' on field Step-Post-Restrict-Delivery-Condition, parameter 'upload.foo' is not valid" }, DisplayName = "Step post restrict-delivery condition is not valid (invalid parameter reference)")]
    [DataRow("invalidStepPostForwardReference_01", new string[] { "PipelineConfig: pipeline 'ili_validation', step 'xtf_matching', invalid expression '[validation.ValidationSuccessful]' on field Step-Post-Fail-Condition, parameter 'validation.ValidationSuccessful' is not valid" }, DisplayName = "Step post condition references a later step")]
    [DataRow("overwriteUndefinedBaseConfig", new string[] { "PipelineProcessConfig: Step 'validation' in pipeline 'ili_validation' is trying to overwrite process config parameter 'validationProfile' which is not defined in the default config." }, DisplayName = "overwrite a undefined base config parameter")]
    [DataRow("duplicateStatusMessageOutput", new string[] { "StepConfig: Step 'validation' has multiple outputs with StatusMessage action. Only one StatusMessage output is allowed per step." }, DisplayName = "Step has multiple StatusMessage outputs")]
    [DataRow("duplicateConditionId", new string[] { "StepConfig: Step 'validation' has duplicate condition ids: not-unique. A condition id must be unique within its step." }, DisplayName = "Step has duplicate condition ids")]

    public void PipelineValidation(string pipelineFile, string[] expectedErrorMessages)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.IsTrue(validationErrors.HasErrors, "expected validation errors but none found");
        var expectedErrorMessage = string.Join(Environment.NewLine, expectedErrorMessages);
        var actualErrorMessage = validationErrors.ErrorMessage;
        Assert.AreEqual(expectedErrorMessage, actualErrorMessage);
    }

    [TestMethod(DisplayName = "Base configuration collision")]
    [DataRow("baseConfigCollisionDefault", new string[] { "PipelineProcessConfig: " + DefaultConfigCollision }, DisplayName = "default_config sets a key the base configuration pins")]
    [DataRow("baseConfigCollisionOverwrite", new string[] { "PipelineProcessConfig: " + DefaultConfigCollision, StepOverwriteCollision }, DisplayName = "default_config and a step overwrite set a key the base configuration pins")]
    [DataRow("baseConfigCollisionNoImplementation", new string[] { "ProcessConfig (Implementation): Process Implementation is required." }, DisplayName = "a process without an implementation is reported as such, not looked up in the base configuration")]
    public void BaseConfigCollision(string pipelineFile, string[] expectedErrorMessages)
    {
        PipelineFactory factory = CreatePipelineFactory(pipelineFile);
        var processBaseConfigs = new Dictionary<string, Parameterization>(StringComparer.Ordinal)
        {
            { XtfValidatorImplementation, new Parameterization { { "validationProfile", "LOCKED" } } },
        };

        var validationErrors = factory.PipelineProcessConfig.Validate(processBaseConfigs);

        Assert.IsTrue(validationErrors.HasErrors, "expected validation errors but none found");
        Assert.AreEqual(string.Join(Environment.NewLine, expectedErrorMessages), validationErrors.ErrorMessage);
    }

    [TestMethod(DisplayName = "The same definition is valid without a base configuration")]
    public void BaseConfigCollisionNeedsABaseConfig()
    {
        PipelineFactory factory = CreatePipelineFactory("baseConfigCollisionOverwrite");

        var validationErrors = factory.PipelineProcessConfig.Validate();

        Assert.IsFalse(validationErrors.HasErrors, validationErrors.ErrorMessage);
    }

    [TestMethod(DisplayName = "A base configuration of another implementation does not collide")]
    public void BaseConfigCollisionIsKeyedByImplementation()
    {
        PipelineFactory factory = CreatePipelineFactory("baseConfigCollisionOverwrite");
        var processBaseConfigs = new Dictionary<string, Parameterization>(StringComparer.Ordinal)
        {
            { "Geopilot.Pipeline.Processes.ZipPackage.ZipPackageProcess", new Parameterization { { "validationProfile", "LOCKED" } } },
        };

        var validationErrors = factory.PipelineProcessConfig.Validate(processBaseConfigs);

        Assert.IsFalse(validationErrors.HasErrors, validationErrors.ErrorMessage);
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        string pipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline");

        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(new Mock<IPipelineProcessFactory>().Object)
            .LoggerFactory(loggerFactoryMock.Object)
            .PipelineTempDirectory(pipelineDirectory)
            .Build();
    }
}
