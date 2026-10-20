using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Stac;
using Stac.Api.Clients.Features;
using Stac.Api.Clients.ItemSearch;

namespace Geopilot.Api.StacApi;

/// <summary>
/// The seed puts no delivery on the public mandate, so this test adds one. Every context of the test host
/// shares the connection and transaction of the test context: data written here is visible to the requests
/// and rolled back afterwards.
/// </summary>
[TestClass]
public class PublicMandateItemsTest
{
    private const string Comment = "Nur für Admins";
    private const string DeclaredByProperty = "Abgegeben durch";

    private Context context;
    private GeopilotApiApp baseApp;
    private WebApplicationFactory<GeopilotApiApp> app;
    private string collectionId;
    private string itemId;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        var mandate = context.Mandates.Single(m => m.StacAccess == StacAccess.Public);
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
        using var adminClient = app.CreateClient();
        adminClient.DefaultRequestHeaders.Add(GeopilotApiApp.AdminHeader, "true");

        var item = await new FeaturesClient(adminClient).GetFeatureAsync(collectionId, itemId);
        var searched = (await new ItemSearchClient(adminClient).PostItemSearchAsync(new SearchBody { Collections = new[] { collectionId } })).Items.Single(i => i.Id == itemId);

        foreach (var adminItem in new[] { item, searched })
        {
            Assert.HasCount(1, adminItem.Assets);
            Assert.IsTrue(adminItem.Properties.ContainsKey(DeclaredByProperty));
            Assert.AreEqual(Comment, adminItem.Description);
        }
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
