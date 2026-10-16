using Geopilot.Pipeline;

namespace Geopilot.Api.Processing;

[TestClass]
public class JobLifecycleTest
{
    public static IEnumerable<object[]> Outcomes =>
        [
            [new RunOutcome.Completed(ProcessingState.Success), ProcessingState.Success, true],
            [new RunOutcome.Completed(ProcessingState.Warning), ProcessingState.Warning, true],
            [new RunOutcome.Completed(ProcessingState.DeliveryRestriction), ProcessingState.DeliveryRestriction, true],
            [new RunOutcome.Completed(ProcessingState.Failed), ProcessingState.Failed, true],
            [new RunOutcome.Completed(ProcessingState.Cancelled), ProcessingState.Cancelled, false],
            [new RunOutcome.TimedOut(), ProcessingState.Cancelled, false],
            [new RunOutcome.ShutDown(), ProcessingState.Cancelled, false],
            [new RunOutcome.Crashed("My Dog ate the Storage Disk"), ProcessingState.Failed, true],
            [new RunOutcome.Completed(ProcessingState.Pending), ProcessingState.Failed, true],
            [new RunOutcome.Completed(ProcessingState.Running), ProcessingState.Failed, true],
        ];

    [TestMethod]
    [DynamicData(nameof(Outcomes))]
    public void DecideFollowsReleaseTable(RunOutcome outcome, ProcessingState expectedState, bool expectedRelease)
    {
        Assert.AreEqual(new JobDecision(expectedState, expectedRelease), JobLifecycle.Decide(outcome));
    }
}
