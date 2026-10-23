using Geopilot.Api.Models;

namespace Geopilot.Api.Reactions;

[TestClass]
public class ForwardingExtensionsTest
{
    [TestMethod]
    [DataRow(new[] { ReactionState.Done, ReactionState.Done }, ReactionState.Done, DisplayName = "All done")]
    [DataRow(new[] { ReactionState.Done, ReactionState.Pending }, ReactionState.Pending, DisplayName = "Pending before done")]
    [DataRow(new[] { ReactionState.Pending, ReactionState.Running }, ReactionState.Running, DisplayName = "Running before pending")]
    [DataRow(new[] { ReactionState.Running, ReactionState.Error, ReactionState.Done }, ReactionState.Error, DisplayName = "Error before running")]
    public void ForwardingStateSumsUpTheTransfers(ReactionState[] transferStates, ReactionState expected)
    {
        Assert.AreEqual(expected, transferStates.ForwardingState());
    }

    [TestMethod]
    public void ForwardingStateIsEmptyWithoutTransfers()
    {
        Assert.IsNull(Array.Empty<ReactionState>().ForwardingState());
    }
}
