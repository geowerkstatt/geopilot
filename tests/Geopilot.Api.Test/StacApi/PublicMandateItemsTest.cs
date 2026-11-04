using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Stac;
using Stac.Api;
using Stac.Api.Clients.Collections;
using Stac.Api.Clients.Features;
using Stac.Api.Clients.ItemSearch;

namespace Geopilot.Api.StacApi;

/// <summary>
/// The seed puts no delivery on the public mandate, so these tests add their own. Every context of the test host
/// shares the connection and transaction of the test context: data written here is visible to the requests
/// and rolled back afterwards.
/// </summary>
[TestClass]
public class PublicMandateItemsTest
{
    private const string Comment = "Nur für Admins";
    private const string DeclaredByProperty = "Abgegeben durch";
    private const string PrecursorProperty = "Vorgängerversion";

    // Far apart, so the time span of the collection shows which deliveries it covers.
    private static readonly DateTime ForwardedDate = new(2020, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UnforwardedDate = new(2001, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private Context context;
    private GeopilotApiApp baseApp;
    private WebApplicationFactory<GeopilotApiApp> app;
    private Mandate mandate;
    private string collectionId;
    private string itemId;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        mandate = context.Mandates.Single(m => m.StacAccess == StacAccess.Public);
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = mandate, DeclaringUser = context.Users.First(), Comment = Comment };
        delivery.Assets.Add(new Asset { OriginalFilename = "Daten.xtf", SanitizedFilename = "daten.xtf", AssetType = AssetType.PrimaryData });
        context.Deliveries.Add(delivery);
        context.SaveChanges();
        collectionId = $"coll_{mandate.Id}";
        itemId = $"item_{delivery.Id}";

        var sharedFactory = new Mock<IDbContextFactory<Context>>();
        sharedFactory.Setup(f => f.CreateDbContext()).Returns(CreateSharedContext);
        baseApp = new GeopilotApiApp();
        app = baseApp.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(sharedFactory.Object)));
    }

    [TestCleanup]
    public void Cleanup()
    {
        app.Dispose();
        baseApp.Dispose();
        context.Dispose();
    }

    [TestMethod]
    public async Task AnonymousGetsItemsOfAPublicMandateWithoutPersonalData()
    {
        using var anonymousClient = app.CreateClient();

        var item = await new FeaturesClient(anonymousClient).GetFeatureAsync(collectionId, itemId);
        var listed = (await new FeaturesClient(anonymousClient).GetFeaturesAsync(collectionId, null, null, null)).Features.Single(f => f.Id == itemId);
        var searched = (await new ItemSearchClient(anonymousClient).PostItemSearchAsync(new SearchBody { Collections = new[] { collectionId } })).Items.Single(i => i.Id == itemId);

        Assert.IsFalse(listed.Properties.ContainsKey(DeclaredByProperty));
        foreach (var anonymousItem in new[] { item, searched })
        {
            Assert.HasCount(1, anonymousItem.Assets);
            Assert.IsFalse(anonymousItem.Properties.ContainsKey(DeclaredByProperty));
            Assert.IsTrue(string.IsNullOrEmpty(anonymousItem.Description));
        }
    }

    [TestMethod]
    public async Task AdminGetsItemsOfAPublicMandateWithPersonalData()
    {
        using var adminClient = CreateClient(asAdmin: true);

        var item = await new FeaturesClient(adminClient).GetFeatureAsync(collectionId, itemId);
        var searched = (await new ItemSearchClient(adminClient).PostItemSearchAsync(new SearchBody { Collections = new[] { collectionId } })).Items.Single(i => i.Id == itemId);

        foreach (var adminItem in new[] { item, searched })
        {
            Assert.HasCount(1, adminItem.Assets);
            Assert.IsTrue(adminItem.Properties.ContainsKey(DeclaredByProperty));
            Assert.AreEqual(Comment, adminItem.Description);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Anonymous")]
    [DataRow(true, DisplayName = "Administrator")]
    public async Task ItemsListOnlyForwardedDeliveries(bool asAdmin)
    {
        var (forwarded, unforwarded) = AddDeliveriesInEveryForwardingState();
        using var client = CreateClient(asAdmin);

        var listed = (await new FeaturesClient(client).GetFeaturesAsync(collectionId, null, null, null)).Features.Select(f => f.Id).ToList();
        var searched = (await new ItemSearchClient(client).PostItemSearchAsync(new SearchBody { Collections = new[] { collectionId } })).Items.Select(i => i.Id).ToList();

        foreach (var ids in new[] { listed, searched })
        {
            CollectionAssert.IsSubsetOf(forwarded, ids);
            Assert.IsEmpty(ids.Intersect(unforwarded));
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Anonymous")]
    [DataRow(true, DisplayName = "Administrator")]
    public async Task AnItemIsFoundOnlyOnceItsDeliveryIsForwarded(bool asAdmin)
    {
        var (forwarded, unforwarded) = AddDeliveriesInEveryForwardingState();
        using var client = CreateClient(asAdmin);
        var features = new FeaturesClient(client);

        foreach (var id in forwarded)
        {
            Assert.AreEqual(id, (await features.GetFeatureAsync(collectionId, id)).Id);
        }

        var nonExistent = await Assert.ThrowsExactlyAsync<StacApiException>(() => features.GetFeatureAsync(collectionId, "item_999999"));
        foreach (var id in unforwarded)
        {
            var hidden = await Assert.ThrowsExactlyAsync<StacApiException>(() => features.GetFeatureAsync(collectionId, id));
            Assert.AreEqual(nonExistent.StatusCode, hidden.StatusCode);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Anonymous")]
    [DataRow(true, DisplayName = "Administrator")]
    public async Task TheCollectionSpansOnlyForwardedDeliveries(bool asAdmin)
    {
        AddDeliveriesInEveryForwardingState();
        using var client = CreateClient(asAdmin);
        var collections = new CollectionsClient(client);

        var described = await collections.DescribeCollectionAsync(collectionId);
        var listed = (await collections.GetCollectionsAsync()).Collections.Single(c => c.Id == collectionId);

        foreach (var collection in new[] { described, listed })
        {
            Assert.AreEqual(ForwardedDate.Year, collection.Extent.Temporal.Interval.Single()[0]?.Year);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Anonymous")]
    [DataRow(true, DisplayName = "Administrator")]
    public async Task AnItemNamesOnlyAForwardedPrecursor(bool asAdmin)
    {
        var afterHidden = AddDelivery(ForwardedDate);
        afterHidden.PrecursorDelivery = AddDelivery(UnforwardedDate, (ReactionKind.Transfer, ReactionState.Error));
        var afterShown = AddDelivery(ForwardedDate);
        afterShown.PrecursorDelivery = AddDelivery(ForwardedDate, (ReactionKind.Transfer, ReactionState.Done));
        context.SaveChanges();
        using var client = CreateClient(asAdmin);
        var features = new FeaturesClient(client);

        Assert.IsFalse((await features.GetFeatureAsync(collectionId, ItemId(afterHidden))).Properties.ContainsKey(PrecursorProperty));
        Assert.IsTrue((await features.GetFeatureAsync(collectionId, ItemId(afterShown))).Properties.ContainsKey(PrecursorProperty));
    }

    // One delivery per forwarding state, as the item ids of those the catalog shows and of those it hides.
    private (List<string> Forwarded, List<string> Unforwarded) AddDeliveriesInEveryForwardingState()
    {
        var forwarded = new[]
        {
            AddDelivery(ForwardedDate),
            AddDelivery(ForwardedDate, (ReactionKind.Transfer, ReactionState.Done), (ReactionKind.Notification, ReactionState.Pending)),
            AddDelivery(ForwardedDate, (ReactionKind.Notification, ReactionState.Error)),
        };
        var unforwarded = new[]
        {
            AddDelivery(UnforwardedDate, (ReactionKind.Transfer, ReactionState.Pending)),
            AddDelivery(UnforwardedDate, (ReactionKind.Transfer, ReactionState.Running)),
            AddDelivery(UnforwardedDate, (ReactionKind.Transfer, ReactionState.Error)),
            AddDelivery(UnforwardedDate, (ReactionKind.Transfer, ReactionState.Done), (ReactionKind.Transfer, ReactionState.Pending)),
        };
        return (forwarded.Select(ItemId).ToList(), unforwarded.Select(ItemId).ToList());
    }

    private static string ItemId(Delivery delivery) => $"item_{delivery.Id}";

    private Delivery AddDelivery(DateTime date, params (ReactionKind Kind, ReactionState State)[] reactions)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = mandate, DeclaringUser = context.Users.First(), Date = date };
        context.Deliveries.Add(delivery);
        context.DeliveryReactions.AddRange(reactions.Select((reaction, index) => new DeliveryReaction
        {
            Delivery = delivery,
            ReactionId = $"reaction-{index}",
            Kind = reaction.Kind,
            Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
            State = reaction.State,
            NextAttemptAt = date,
        }));
        context.SaveChanges();
        return delivery;
    }

    private HttpClient CreateClient(bool asAdmin)
    {
        var client = app.CreateClient();
        if (asAdmin)
        {
            client.DefaultRequestHeaders.Add(GeopilotApiApp.AdminHeader, "true");
        }

        return client;
    }

    private Context CreateSharedContext()
    {
        var options = new DbContextOptionsBuilder<Context>()
            .UseNpgsql(context.Database.GetDbConnection(), o =>
            {
                o.UseNetTopologySuite();
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .Options;
        var sharedContext = new Context(options);
        sharedContext.Database.UseTransaction((context.Database.CurrentTransaction ?? throw new InvalidOperationException("The test context has no transaction.")).GetDbTransaction());
        return sharedContext;
    }
}
