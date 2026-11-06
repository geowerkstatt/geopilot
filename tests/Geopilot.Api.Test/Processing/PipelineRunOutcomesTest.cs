using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class PipelineRunOutcomesTest
{
    [TestMethod]
    public void DeliveredWhenALiveDeliveryExists()
    {
        var run = Run(ProcessingState.Success, userId: 1);

        var outcome = PipelineRunOutcomes.Determine(run, isRunning: false, new PipelineRunDelivery(7, Deleted: false, ReactionState.Error));

        Assert.AreEqual(PipelineRunOutcomeKind.Delivered, outcome.Kind);
        Assert.AreEqual(7, outcome.DeliveryId);
        Assert.AreEqual(ReactionState.Error, outcome.ForwardingState);
    }

    [TestMethod]
    public void DeliveredAndDeletedWhenOnlyADeletedDeliveryExists()
    {
        var run = Run(ProcessingState.Success, userId: 1);

        var outcome = PipelineRunOutcomes.Determine(run, isRunning: false, new PipelineRunDelivery(7, Deleted: true, ForwardingState: null));

        Assert.AreEqual(PipelineRunOutcomeKind.DeliveredAndDeleted, outcome.Kind);
        Assert.AreEqual(7, outcome.DeliveryId);
    }

    [TestMethod]
    public void RunningWhileTheJobIsActive()
    {
        var outcome = PipelineRunOutcomes.Determine(Run(terminalState: null, userId: 1), isRunning: true, delivery: null);

        Assert.AreEqual(PipelineRunOutcomeKind.Running, outcome.Kind);
    }

    [TestMethod]
    public void UnknownWithoutTerminalStateAndActiveJob()
    {
        var outcome = PipelineRunOutcomes.Determine(Run(terminalState: null, userId: 1), isRunning: false, delivery: null);

        Assert.AreEqual(PipelineRunOutcomeKind.Unknown, outcome.Kind, "a run without terminal state whose job is gone died with its instance.");
    }

    [TestMethod]
    [DataRow(ProcessingState.Success)]
    [DataRow(ProcessingState.Warning)]
    public void NotDeliveredWhenDeliverableWithoutDelivery(ProcessingState terminalState)
    {
        var outcome = PipelineRunOutcomes.Determine(Run(terminalState, userId: 1), isRunning: false, delivery: null);

        Assert.AreEqual(PipelineRunOutcomeKind.NotDelivered, outcome.Kind);
    }

    [TestMethod]
    public void NotDeclarableWhenAnonymousAndDeliverable()
    {
        var outcome = PipelineRunOutcomes.Determine(Run(ProcessingState.Success), isRunning: false, delivery: null);

        Assert.AreEqual(PipelineRunOutcomeKind.NotDeclarable, outcome.Kind, "nobody who processes without signing in may deliver.");
    }

    [TestMethod]
    public void NotDeliverableNamesEveryBlockingStepInPipelineOrder()
    {
        // A delivery restriction does not stop the pipeline, so several steps can restrict the delivery of one run.
        var run = Run(
            ProcessingState.DeliveryRestriction,
            userId: 1,
            Step("a", StepState.Success, order: 0),
            Step("b", StepState.DeliveryRestriction, order: 3),
            Step("c", StepState.DeliveryRestriction, order: 1),
            Step("d", StepState.Warning, order: 2));

        var outcome = PipelineRunOutcomes.Determine(run, isRunning: false, delivery: null);

        Assert.AreEqual(PipelineRunOutcomeKind.NotDeliverable, outcome.Kind);
        CollectionAssert.AreEqual(new[] { "c", "b" }, outcome.BlockingSteps.Select(step => step.StepId).ToArray(), "the steps are inserted out of pipeline order on purpose.");
        Assert.AreEqual("c", outcome.BlockingSteps[0].DisplayName["de"]);
    }

    [TestMethod]
    public void NotDeliverableNamesAFailedStepAfterARestrictingOne()
    {
        var run = Run(
            ProcessingState.Failed,
            userId: 1,
            Step("a", StepState.DeliveryRestriction, order: 0),
            Step("b", StepState.Error, order: 1),
            Step("c", StepState.Pending, order: 2));

        var outcome = PipelineRunOutcomes.Determine(run, isRunning: false, delivery: null);

        CollectionAssert.AreEqual(new[] { "a", "b" }, outcome.BlockingSteps.Select(step => step.StepId).ToArray());
    }

    [TestMethod]
    public void NotDeliverableWithoutStepWhenNoStepBlocked()
    {
        var run = Run(ProcessingState.Failed, userId: 1, Step("a", StepState.Success, order: 0), Step("b", StepState.Pending, order: 1));

        var outcome = PipelineRunOutcomes.Determine(run, isRunning: false, delivery: null);

        Assert.AreEqual(PipelineRunOutcomeKind.NotDeliverable, outcome.Kind);
        Assert.HasCount(0, outcome.BlockingSteps, "a run that failed in the preflight has no step to blame.");
    }

    [TestMethod]
    public void OnlyANotDeliverableRunNamesBlockingSteps()
    {
        var run = Run(ProcessingState.Success, userId: 1, Step("a", StepState.Success, order: 0));

        var outcome = PipelineRunOutcomes.Determine(run, isRunning: false, delivery: null);

        Assert.HasCount(0, outcome.BlockingSteps);
    }

    private static PipelineRun Run(ProcessingState? terminalState, int? userId = null, params PipelineRunStep[] steps) => new()
    {
        JobId = Guid.NewGuid(),
        TerminalState = terminalState,
        UserId = userId,
        Steps = steps.ToList(),
    };

    private static PipelineRunStep Step(string id, StepState state, int order) => new()
    {
        StepId = id,
        State = state,
        Order = order,
        DisplayName = TestHelpers.Localized(id),
    };
}
