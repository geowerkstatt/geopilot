using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Moq;

namespace Geopilot.Api;

[TestClass]
public class DtoMapperExtensionsTest
{
    private static readonly Func<Guid, string, Uri> BuildDownloadUrl =
        (id, file) => new Uri($"https://localhost/api/v2/processing/{id}/files/{file}");

    private static readonly Func<Guid, string, Uri> BuildVisualizationUrl =
        (id, file) => new Uri($"https://localhost/api/v2/processing/{id}/visualizations/{file}");

    [TestMethod]
    public void PreflightStepLeadsThePipelineSteps()
    {
        var job = BuildJob(ProcessingState.Pending, BuildPipeline(ProcessingState.Pending, "step_a", "step_b"));

        var response = job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl);

        Assert.HasCount(3, response.Steps);
        Assert.AreEqual(DtoMapperExtensions.PreflightStepId, response.Steps[0].Id);
        Assert.AreEqual("step_a", response.Steps[1].Id);
        Assert.AreEqual("step_b", response.Steps[2].Id);
    }

    [TestMethod]
    public void PreflightStepIsLocalizedAndCarriesNoContent()
    {
        var job = BuildJob(ProcessingState.Pending, BuildPipeline(ProcessingState.Pending));

        var preflight = job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl).Steps[0];

        Assert.AreEqual("Vorbereitung", preflight.Name["de"]);
        Assert.AreEqual("Preparation", preflight.Name["en"]);
        Assert.AreEqual("Préparation", preflight.Name["fr"]);
        Assert.AreEqual("Preparazione", preflight.Name["it"]);
        Assert.IsNull(preflight.StatusMessage);
        Assert.IsEmpty(preflight.Downloads);
        Assert.IsEmpty(preflight.Visualizations);
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending, ProcessingState.Pending, StepState.Running)]
    [DataRow(ProcessingState.Running, ProcessingState.Running, StepState.Success)]
    [DataRow(ProcessingState.Success, ProcessingState.Success, StepState.Success)]
    [DataRow(ProcessingState.Warning, ProcessingState.Warning, StepState.Success)]
    [DataRow(ProcessingState.DeliveryRestriction, ProcessingState.DeliveryRestriction, StepState.Success)]
    [DataRow(ProcessingState.Cancelled, ProcessingState.Cancelled, StepState.Success)]
    [DataRow(ProcessingState.Failed, ProcessingState.Pending, StepState.Error)]
    [DataRow(ProcessingState.Failed, ProcessingState.Failed, StepState.Success)]
    public void PreflightStateReflectsJobLifecycle(ProcessingState jobState, ProcessingState pipelineState, StepState expected)
    {
        var job = BuildJob(jobState, BuildPipeline(pipelineState));

        var preflight = job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl).Steps[0];

        Assert.AreEqual(expected, preflight.State);
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending, ProcessingState.Pending, "Pending", "Running,Pending")]
    [DataRow(ProcessingState.Running, ProcessingState.Pending, "Pending,Pending", "Running,Pending,Pending")]
    [DataRow(ProcessingState.Running, ProcessingState.Running, "Running,Pending", "Success,Running,Pending")]
    [DataRow(ProcessingState.Running, ProcessingState.Running, "Success,Pending", "Success,Running,Pending")]
    [DataRow(ProcessingState.Running, ProcessingState.Running, "Success,Skipped,Pending", "Success,Success,Running,Pending")]
    [DataRow(ProcessingState.Running, ProcessingState.Success, "Success,Success", "Success,Success,Running")]
    [DataRow(ProcessingState.Success, ProcessingState.Success, "Success,Success", "Success,Success,Success")]
    [DataRow(ProcessingState.Failed, ProcessingState.Failed, "Error,Pending", "Success,Error,Pending")]
    public void FinishedStepIsReportedRunningUntilItsSuccessorStarts(ProcessingState jobState, ProcessingState pipelineState, string stepStates, string expectedStates)
    {
        var states = stepStates.Split(',').Select(Enum.Parse<StepState>).ToArray();
        var stepIds = states.Select((_, index) => $"step_{index}").ToArray();
        var job = BuildJob(jobState, BuildPipeline(pipelineState, stepIds, states));

        var response = job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl);
        var reported = response.WithRunningStepWhileJobRuns().Steps.Select(step => step.State.ToString());

        Assert.AreEqual(expectedStates, string.Join(',', reported));
        Assert.AreEqual(stepStates, string.Join(',', response.Steps.Skip(1).Select(step => step.State.ToString())), "The plain mapping reports the actual step states, which the machine delivery derives its message severity from.");
    }

    [TestMethod]
    public void PreflightStepIsReportedEvenWithoutAPipeline()
    {
        var pending = BuildJob(ProcessingState.Pending, pipeline: null);
        var failed = BuildJob(ProcessingState.Failed, pipeline: null);

        var pendingResponse = pending.ToResponse(BuildDownloadUrl, BuildVisualizationUrl);
        var failedResponse = failed.ToResponse(BuildDownloadUrl, BuildVisualizationUrl);

        Assert.HasCount(1, pendingResponse.Steps);
        Assert.AreEqual(StepState.Running, pendingResponse.Steps[0].State);
        Assert.HasCount(1, failedResponse.Steps);
        Assert.AreEqual(StepState.Error, failedResponse.Steps[0].State);
    }

    [TestMethod]
    public void PreflightFailureStatusMessageIsLocalizedAndContainsTheJobId()
    {
        var job = BuildJob(ProcessingState.Failed, BuildPipeline(ProcessingState.Pending));

        var preflight = job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl).Steps[0];

        Assert.AreEqual(StepState.Error, preflight.State);
        var statusMessage = preflight.StatusMessage;
        Assert.IsNotNull(statusMessage);
        foreach (var language in new[] { "de", "en", "fr", "it" })
        {
            var text = statusMessage[language];
            Assert.IsNotNull(text, $"Missing preflight failure message for <{language}>.");
            Assert.IsTrue(
                text.Contains(job.Id.ToString(), StringComparison.Ordinal),
                $"Preflight failure message for <{language}> must contain the job id.");
        }
    }

    [TestMethod]
    public void PreflightCarriesNoStatusMessageWhenThePipelineFailed()
    {
        var job = BuildJob(ProcessingState.Failed, BuildPipeline(ProcessingState.Failed));

        var preflight = job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl).Steps[0];

        Assert.AreEqual(StepState.Success, preflight.State);
        Assert.IsNull(preflight.StatusMessage);
    }

    private static ProcessingJob BuildJob(ProcessingState state, IPipeline? pipeline) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.Now) { Pipeline = pipeline, State = state };

    private static IPipeline BuildPipeline(ProcessingState state, params string[] stepIds) =>
        BuildPipeline(state, stepIds, stepIds.Select(_ => StepState.Pending).ToArray());

    private static IPipeline BuildPipeline(ProcessingState state, string[] stepIds, StepState[] stepStates)
    {
        var steps = stepIds.Zip(stepStates, BuildStep).ToList();
        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.DisplayName).Returns(LocalizedText.Empty);
        pipelineMock.SetupGet(p => p.State).Returns(state);
        pipelineMock.SetupGet(p => p.Steps).Returns(steps);
        return pipelineMock.Object;
    }

    private static IPipelineStep BuildStep(string id, StepState state)
    {
        var stepMock = new Mock<IPipelineStep>();
        stepMock.SetupGet(s => s.Id).Returns(id);
        stepMock.SetupGet(s => s.DisplayName).Returns(LocalizedText.Empty);
        stepMock.SetupGet(s => s.State).Returns(state);
        stepMock.SetupGet(s => s.StatusMessage).Returns((LocalizedText?)null);
        stepMock.SetupGet(s => s.Downloads).Returns(new List<PersistedFile>());
        stepMock.SetupGet(s => s.DeliveryFiles).Returns(new List<PersistedFile>());
        stepMock.SetupGet(s => s.Visualizations).Returns(new List<StepVisualization>());
        return stepMock.Object;
    }
}
