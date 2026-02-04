using Geopilot.Api.Authorization;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Microsoft.OpenApi;
using Moq;
using System.Linq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
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
    public void RunTwoStepPipeline()
    {
        var uploadStepId = "upload";
        var validationStepId = "validation";
        var dummyStepId = "dummy";
        var uploadedFileAttribute = "ili_file";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");
        var pipeline = factory.CreatePipeline("two_steps");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(2, pipeline.Steps);

        using var fileHandle = CreateTestFileHandle("TestData/UploadFiles/RoadsExdm2ien.xtf");
        var context = pipeline.Run(fileHandle);

        Assert.AreEqual(PipelineState.Success, pipeline.State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[1].State);

        // Assert if uploaded file was correctly added to PipelineContext
        var stepResults = context.StepResults;

        Assert.IsTrue(stepResults.ContainsKey(uploadStepId));
        var uploadStepResult = context.StepResults[uploadStepId];
        Assert.HasCount(1, uploadStepResult.Outputs, "upload step has not the expected number of data");
        Assert.IsTrue(uploadStepResult.Outputs.ContainsKey(uploadedFileAttribute));
        var uploadedFileStepOutput = uploadStepResult.Outputs[uploadedFileAttribute];

        Assert.IsNotNull(uploadedFileStepOutput.Data);
        var uploadedFile = uploadedFileStepOutput.Data as FileHandle;
        Assert.IsNotNull(uploadedFile);
        Assert.AreEqual(fileHandle.FileName, uploadedFile.FileName);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(3, stepResults);
        Assert.IsTrue(stepResults.ContainsKey(validationStepId));
        var validationSetpResult = stepResults[validationStepId];
        Assert.HasCount(2, validationSetpResult.Outputs, "validation step has not the expected number of data");

        Assert.IsTrue(stepResults.ContainsKey(dummyStepId));
        var dummyStepResult = stepResults[dummyStepId];
        Assert.HasCount(1, dummyStepResult.Outputs, "dummy step has not the expected number of data");
    }

    [TestMethod(DisplayName = "Pipeline State Test")]
    [DataRow(PipelineState.Pending, new[] { StepState.Pending, StepState.Pending })]
    [DataRow(PipelineState.Running, new[] { StepState.Running, StepState.Pending })]
    [DataRow(PipelineState.Pending, new StepState[0])]
    [DataRow(PipelineState.Success, new[] { StepState.Success, StepState.Warning, StepState.Pending })]
    [DataRow(PipelineState.Success, new[] { StepState.Success, StepState.Warning, StepState.Success })]
    [DataRow(PipelineState.Failed, new[] { StepState.Success, StepState.Failed, StepState.Pending })]
    public void PipelineStateTest(PipelineState expectedState, IEnumerable<StepState> stepStates)
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };
        var inputConfigs = new List<InputConfig>();
        var outputConfigs = new List<OutputConfig>();

        var steps = stepStates
            .Select(s =>
            {
                var step = new Mock<IPipelineStep>();
                step.SetupProperty(s => s.State, s);
                return step.Object;
            })
            .ToList();

        var pipelineParameters = new PipelineParametersConfig() { UploadStep = "upload", Mappings = new List<FileMappingsConfig>() };

        var pipeline = new Api.Pipeline.Pipeline("test_pipeline", pipelineDisplayName, steps, pipelineParameters);

        Assert.AreEqual(expectedState, pipeline.State);
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        return PipelineFactory.FromFile(path);
    }

    private FileHandle CreateTestFileHandle(string file)
    {
        var tempFilePath = Path.GetTempFileName();
        var stream = File.Open(file, FileMode.Open, System.IO.FileAccess.Read);
        return new FileHandle(file, stream);
    }
}
