using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Reactions;

[TestClass]
public class ReactionStoreTest
{
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(10);

    private Context context;
    private ReactionStore store;
    private DateTime dbNow;
    private Delivery delivery;

    [TestInitialize]
    public async Task Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();

        // Two waits between attempts make three attempts.
        store = new ReactionStore(Options.Create(new DeliveryReactionOptions
        {
            RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)],
            TimeLimit = TimeLimit,
        }));

        // now() stands still within the test transaction, so every time the store writes has to equal this one.
        dbNow = await context.Database.SqlQuery<DateTime>($"SELECT now() AS \"Value\"").SingleAsync();
        delivery = AddDelivery();
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task ClaimNextStartsTheAttemptOfADueReaction()
    {
        var reaction = AddReaction(nextAttemptAt: dbNow.AddSeconds(-1));

        var claimed = await ClaimAsync();

        Assert.AreEqual(reaction.Id, claimed.Id);
        Assert.AreEqual(ReactionState.Running, claimed.State);
        Assert.AreEqual(1, claimed.Attempts);
        Assert.IsNotNull(claimed.ClaimToken);
        Assert.AreEqual(dbNow, claimed.LastAttemptAt);
        Assert.AreEqual(dbNow + TimeLimit + ReactionStore.LockMargin, claimed.LockedUntil);
    }

    [TestMethod]
    public async Task ClaimNextLeavesAReactionOnHold()
    {
        AddReaction(nextAttemptAt: dbNow.AddMinutes(1));

        Assert.IsNull(await store.ClaimNextAsync(context, CancellationToken.None));
    }

    [TestMethod]
    public async Task ClaimNextTakesTheReactionDueLongestFirst()
    {
        AddReaction("later", nextAttemptAt: dbNow.AddMinutes(-1));
        var first = AddReaction("first", nextAttemptAt: dbNow.AddMinutes(-2));

        Assert.AreEqual(first.Id, (await ClaimAsync()).Id);
    }

    [TestMethod]
    public async Task ClaimNextLeavesARunningReactionWithinItsLock()
    {
        AddReaction(state: ReactionState.Running, attempts: 1, lockedUntil: dbNow.AddMinutes(1));

        Assert.IsNull(await store.ClaimNextAsync(context, CancellationToken.None));
    }

    [TestMethod]
    public async Task ClaimNextTakesOverARunningReactionOnceItsLockPassed()
    {
        var reaction = AddReaction(state: ReactionState.Running, attempts: 1, lockedUntil: dbNow.AddSeconds(-1));

        var claimed = await ClaimAsync();

        Assert.AreEqual(reaction.Id, claimed.Id);
        Assert.AreEqual(2, claimed.Attempts);
        Assert.AreNotEqual(reaction.ClaimToken, claimed.ClaimToken);
    }

    [TestMethod]
    public async Task ClaimNextGivesUpARunningReactionOnceTheLockOfItsLastAttemptPassed()
    {
        var reaction = AddReaction(state: ReactionState.Running, attempts: 3, lockedUntil: dbNow.AddSeconds(-1));

        Assert.IsNull(await store.ClaimNextAsync(context, CancellationToken.None));

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Error, stored.State);
        Assert.AreEqual(dbNow, stored.FinishedAt);
        Assert.IsNull(stored.LockedUntil);
        Assert.IsNull(stored.ClaimToken);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stored.Message));
    }

    [TestMethod]
    [DataRow(ReactionState.Done)]
    [DataRow(ReactionState.Error)]
    public async Task ClaimNextLeavesAFinishedReaction(ReactionState state)
    {
        AddReaction(state: state, attempts: 1, nextAttemptAt: dbNow.AddMinutes(-1));

        Assert.IsNull(await store.ClaimNextAsync(context, CancellationToken.None));
    }

    [TestMethod]
    public async Task ClaimNextLeavesTheReactionsOfADeletedDelivery()
    {
        AddReaction(nextAttemptAt: dbNow.AddMinutes(-1));
        delivery.Deleted = true;
        await context.SaveChangesAsync();

        Assert.IsNull(await store.ClaimNextAsync(context, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(ReactionState.Pending, false)]
    [DataRow(ReactionState.Running, false)]
    [DataRow(ReactionState.Error, false)]
    [DataRow(ReactionState.Done, true)]
    public async Task ClaimNextStartsANotificationOnceEveryTransferIsDone(ReactionState transferState, bool claimable)
    {
        AddTransferThatIsNotDue(transferState);
        var notification = AddReaction("notification", kind: ReactionKind.Notification, nextAttemptAt: dbNow.AddMinutes(-1));

        var claimed = await store.ClaimNextAsync(context, CancellationToken.None);

        Assert.AreEqual(claimable ? notification.Id : null, claimed?.Id);
    }

    [TestMethod]
    public async Task ClaimNextStartsANotificationOfADeliveryWithoutTransfers()
    {
        var notification = AddReaction("notification", kind: ReactionKind.Notification, nextAttemptAt: dbNow.AddMinutes(-1));

        Assert.AreEqual(notification.Id, (await ClaimAsync()).Id);
    }

    [TestMethod]
    public async Task ClaimNextJudgesANotificationByTheTransfersOfItsOwnDelivery()
    {
        AddTransferThatIsNotDue(ReactionState.Error);
        var notification = AddReaction("notification", kind: ReactionKind.Notification, nextAttemptAt: dbNow.AddMinutes(-1), of: AddDelivery());

        Assert.AreEqual(notification.Id, (await ClaimAsync()).Id);
    }

    [TestMethod]
    public async Task CompleteFinishesTheAttempt()
    {
        AddReaction(nextAttemptAt: dbNow.AddMinutes(-1));
        var claimed = await ClaimAsync();

        Assert.IsTrue(await store.CompleteAsync(context, claimed, "Stored in mandate-a/42.", CancellationToken.None));

        var stored = await ReloadAsync(claimed);
        Assert.AreEqual(ReactionState.Done, stored.State);
        Assert.AreEqual(dbNow, stored.FinishedAt);
        Assert.AreEqual("Stored in mandate-a/42.", stored.Message);
        Assert.IsNull(stored.LockedUntil);
        Assert.IsNull(stored.ClaimToken);
    }

    [TestMethod]
    public async Task CompleteRefusesAnAttemptThatLostTheReaction()
    {
        AddReaction(nextAttemptAt: dbNow.AddMinutes(-1));
        var claimed = await ClaimAsync();

        Assert.IsFalse(await store.CompleteAsync(context, LostAttemptOf(claimed), null, CancellationToken.None));

        Assert.AreEqual(ReactionState.Running, (await ReloadAsync(claimed)).State);
    }

    [TestMethod]
    [DataRow(0, 1, DisplayName = "After the first attempt")]
    [DataRow(1, 5, DisplayName = "After the second attempt")]
    public async Task FailSchedulesTheNextAttempt(int previousAttempts, int expectedWaitMinutes)
    {
        AddReaction(attempts: previousAttempts, nextAttemptAt: dbNow.AddMinutes(-1));
        var claimed = await ClaimAsync();

        Assert.IsTrue(await store.FailAsync(context, claimed, "The target is not reachable.", CancellationToken.None));

        var stored = await ReloadAsync(claimed);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual(dbNow.AddMinutes(expectedWaitMinutes), stored.NextAttemptAt);
        Assert.AreEqual("The target is not reachable.", stored.Message);
        Assert.IsNull(stored.FinishedAt);
        Assert.IsNull(stored.LockedUntil);
        Assert.IsNull(stored.ClaimToken);
    }

    [TestMethod]
    [DataRow(30, DisplayName = "Attempt shorter than the wait")]
    [DataRow(600, DisplayName = "Attempt longer than the wait")]
    public async Task FailCountsTheWaitFromTheStartOfTheFailedAttempt(int startedSecondsAgo)
    {
        var startedAt = dbNow.AddSeconds(-startedSecondsAgo);
        var running = AddReaction(state: ReactionState.Running, attempts: 1, lockedUntil: dbNow.AddMinutes(10), lastAttemptAt: startedAt);

        Assert.IsTrue(await store.FailAsync(context, running, "The target is not reachable.", CancellationToken.None));

        Assert.AreEqual(startedAt.AddMinutes(1), (await ReloadAsync(running)).NextAttemptAt);
    }

    [TestMethod]
    public async Task FailGivesUpAfterTheLastAttempt()
    {
        AddReaction(attempts: 2, nextAttemptAt: dbNow.AddMinutes(-1));
        var claimed = await ClaimAsync();

        Assert.IsTrue(await store.FailAsync(context, claimed, "The target is not reachable.", CancellationToken.None));

        var stored = await ReloadAsync(claimed);
        Assert.AreEqual(3, stored.Attempts);
        Assert.AreEqual(ReactionState.Error, stored.State);
        Assert.AreEqual(dbNow, stored.FinishedAt);
        Assert.AreEqual("The target is not reachable.", stored.Message);
        Assert.IsNull(stored.LockedUntil);
        Assert.IsNull(stored.ClaimToken);
    }

    [TestMethod]
    public async Task FailRefusesAnAttemptThatLostTheReaction()
    {
        AddReaction(nextAttemptAt: dbNow.AddMinutes(-1));
        var claimed = await ClaimAsync();

        Assert.IsFalse(await store.FailAsync(context, LostAttemptOf(claimed), "Too late.", CancellationToken.None));

        Assert.AreEqual(ReactionState.Running, (await ReloadAsync(claimed)).State);
    }

    private static DeliveryReaction LostAttemptOf(DeliveryReaction claimed)
        => new() { Id = claimed.Id, Attempts = claimed.Attempts, ClaimToken = Guid.NewGuid() };

    private async Task<DeliveryReaction> ClaimAsync()
    {
        var claimed = await store.ClaimNextAsync(context, CancellationToken.None);
        Assert.IsNotNull(claimed);
        return claimed;
    }

    private Task<DeliveryReaction> ReloadAsync(DeliveryReaction reaction)
        => context.DeliveryReactions.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == reaction.Id);

    private Delivery AddDelivery()
    {
        var newDelivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeclaringUser = context.Users.First() };
        context.Deliveries.Add(newDelivery);
        context.SaveChanges();
        return newDelivery;
    }

    // A transfer the store never claims itself: on hold, within its lock, or finished.
    private void AddTransferThatIsNotDue(ReactionState state)
        => AddReaction("transfer", state: state, attempts: state == ReactionState.Pending ? 0 : 1, nextAttemptAt: dbNow.AddMinutes(1), lockedUntil: dbNow.AddMinutes(1));

    private DeliveryReaction AddReaction(
        string reactionId = "drop",
        ReactionState state = ReactionState.Pending,
        int attempts = 0,
        DateTime? nextAttemptAt = null,
        DateTime? lockedUntil = null,
        DateTime? lastAttemptAt = null,
        ReactionKind kind = ReactionKind.Transfer,
        Delivery? of = null)
    {
        var running = state == ReactionState.Running;
        var reaction = new DeliveryReaction
        {
            Delivery = of ?? delivery,
            ReactionId = reactionId,
            Kind = kind,
            Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
            State = state,
            Attempts = attempts,
            NextAttemptAt = nextAttemptAt ?? dbNow,
            LockedUntil = running ? lockedUntil : null,
            ClaimToken = running ? Guid.NewGuid() : null,
            LastAttemptAt = running ? lastAttemptAt ?? dbNow : null,
        };
        context.DeliveryReactions.Add(reaction);
        context.SaveChanges();
        return reaction;
    }
}
