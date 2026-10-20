using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Reactions;

[TestClass]
[DoNotParallelize]
public class ReactionStoreConcurrencyTest
{
    // A database of its own: these tests need several connections that see each other's commits.
    private static readonly string ConnectionString = TestDatabaseFixture.ConnectionString.Replace(
        "Database=geopilot-test",
        "Database=geopilot-reaction-store-test",
        StringComparison.Ordinal);

    private static readonly ReactionStore Store = new(Options.Create(new DeliveryReactionOptions()));

    [ClassInitialize]
    public static void CreateDatabase(TestContext testContext)
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
        context.Database.Migrate();

        context.Users.Add(CreateUser("reaction-store-test", "Reaction Store", "reaction-store@example.com"));
        context.Mandates.Add(new Mandate { Name = Localized("Reaction store") });
        context.SaveChanges();
    }

    [ClassCleanup]
    public static void DropDatabase()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
    }

    [TestMethod]
    public async Task TryClaimLetsOnlyTheFirstOfTwoConcurrentClaimsWin()
    {
        var reactionId = (await AddDueReactionsAsync(1)).Single();

        await using var first = CreateContext();
        await using var transaction = await first.Database.BeginTransactionAsync();
        Assert.IsTrue(await Store.TryClaimAsync(first, reactionId, Guid.NewGuid(), CancellationToken.None));

        // The second claim starts while the first is not committed yet, so it has to wait for the lock of the row.
        await using var second = CreateContext();
        var secondClaim = Store.TryClaimAsync(second, reactionId, Guid.NewGuid(), CancellationToken.None);
        await WaitUntilASessionWaitsForALockAsync();
        await transaction.CommitAsync();

        Assert.IsFalse(await secondClaim);
    }

    [TestMethod]
    public async Task ClaimNextHandsEveryReactionToExactlyOneOfManyInstances()
    {
        var reactionIds = await AddDueReactionsAsync(20);

        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(ClaimUntilNoneIsDueAsync)));

        CollectionAssert.AreEquivalent(reactionIds, claims.SelectMany(claimed => claimed).ToList());
    }

    private static async Task<List<int>> ClaimUntilNoneIsDueAsync()
    {
        var claimed = new List<int>();
        await using var context = CreateContext();
        while (await Store.ClaimNextAsync(context, CancellationToken.None) is { } reaction)
        {
            claimed.Add(reaction.Id);
        }

        return claimed;
    }

    private static async Task<List<int>> AddDueReactionsAsync(int count)
    {
        await using var context = CreateContext();
        var delivery = new Delivery
        {
            JobId = Guid.NewGuid(),
            Mandate = await context.Mandates.FirstAsync(),
            DeclaringUser = await context.Users.FirstAsync(),
        };
        var reactions = Enumerable.Range(0, count)
            .Select(index => new DeliveryReaction
            {
                Delivery = delivery,
                ReactionId = $"drop-{index}",
                Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
                NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            })
            .ToList();
        context.DeliveryReactions.AddRange(reactions);
        await context.SaveChangesAsync();
        return reactions.Select(reaction => reaction.Id).ToList();
    }

    private static async Task WaitUntilASessionWaitsForALockAsync()
    {
        await using var observer = CreateContext();
        for (var poll = 0; poll < 200; poll++)
        {
            var waiting = await observer.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'")
                .SingleAsync();
            if (waiting > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("The second claim never waited for the lock of the first one.");
    }

    private static Context CreateContext()
        => new(new DbContextOptionsBuilder<Context>().UseNpgsql(ConnectionString, o => o.UseNetTopologySuite()).Options);
}
