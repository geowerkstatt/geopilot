using Geopilot.Api.Models;
using Microsoft.AspNetCore.StaticFiles;
using Moq;
using NetTopologySuite.Geometries;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.Models;
using Stac.Api.WebApi.Services;

namespace Geopilot.Api.StacServices;

[TestClass]
public class StacConverterTest
{
    private static readonly double[] mandateExtent = new double[] { 7.536621, 46.521076, 9.398804, 47.476376 };

    private Mandate mandate;
    private Delivery testDelivery;
    private Mock<IContentTypeProvider> contentTypeProviderMock;
    private Mock<IStacApiContext> contextMock;
    private Mock<IStacApiContextFactory> contextFactoryMock;
    private Mock<IStacLinker> stacLinkerMock;
    private StacConverter converter;

    [TestInitialize]
    public void Initialize()
    {
        stacLinkerMock = new Mock<IStacLinker>(MockBehavior.Strict);
        contextMock = new Mock<IStacApiContext>(MockBehavior.Strict);
        contextFactoryMock = new Mock<IStacApiContextFactory>(MockBehavior.Strict);
        contentTypeProviderMock = new Mock<IContentTypeProvider>(MockBehavior.Strict);
        converter = new StacConverter(stacLinkerMock.Object, contextFactoryMock.Object, contentTypeProviderMock.Object);
        mandate = new()
        {
            Id = 1,
            Name = "Test Mandate",
            SpatialExtent = new Polygon(new LinearRing(new NetTopologySuite.Geometries.Coordinate[]
            {
                    new(mandateExtent[1], mandateExtent[0]),
                    new(mandateExtent[3], mandateExtent[0]),
                    new(mandateExtent[3], mandateExtent[2]),
                    new(mandateExtent[1], mandateExtent[2]),
                    new(mandateExtent[1], mandateExtent[0]),
            })),
        };
        testDelivery = new()
        {
            Id = 1,
            Date = new DateTime(2023, 11, 6, 10, 45, 18),
            DeclaringUser = new User()
            {
                Id = 2,
            },
            Mandate = mandate,
            Assets = new List<Asset>()
            {
                new Asset()
                {
                    Id = 4,
                    OriginalFilename = "TestFile.xtf",
                    AssetType = AssetType.PrimaryData,
                },
                new Asset()
                {
                    Id = 5,
                    OriginalFilename = "log.txt",
                    AssetType = AssetType.ValidationReport,
                },
            },
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        contentTypeProviderMock.VerifyAll();
        contextFactoryMock.VerifyAll();
        stacLinkerMock.VerifyAll();
    }

    [TestMethod]
    public void GetCollectionId()
    {
        Assert.AreEqual("coll_1", converter.GetCollectionId(mandate));
    }

    [TestMethod]
    public void GetItemId()
    {
        Assert.AreEqual("item_1", converter.GetItemId(testDelivery));
    }

    [TestMethod]
    public void ConvertToStacCollectionWithoutItems()
    {
        var collection = converter.ToStacCollection(mandate);
        Assert.IsNotNull(collection, "StacCollection should not be null.");
        Assert.AreEqual(converter.GetCollectionId(mandate), collection.Id);
        Assert.AreEqual("Test Mandate", collection.Title);
        Assert.AreEqual(string.Empty, collection.Description);
        Assert.IsEmpty(collection.Links);
        var actualExtent = collection.Extent.Spatial.BoundingBoxes[0];
        Assert.AreEqual(mandateExtent[0], actualExtent[1]);
        Assert.AreEqual(mandateExtent[1], actualExtent[0]);
        Assert.AreEqual(mandateExtent[2], actualExtent[3]);
        Assert.AreEqual(mandateExtent[3], actualExtent[2]);
    }

    private void SetupContextMocks()
    {
        stacLinkerMock.Setup(linker => linker.Link(It.IsAny<StacItem>(), It.IsAny<IStacApiContext>()))
            .Callback((StacItem item, IStacApiContext context) =>
            {
                item.Links.Add(new StacApiLink(
                    new Uri("https://iamalink.com"),
                    "self",
                    "Title",
                    "application/octet-stream"));
            });
        contextMock.SetupGet(context => context.BaseUri).Returns(new Uri("https://localhost:5173/"));
        contextFactoryMock.Setup(factory => factory.Create()).Returns(contextMock.Object);
        var contentType = "mocked/contenttype";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(It.IsAny<string>(), out contentType)).Returns(true);
    }

    [TestMethod]
    public void ConvertToStacCollectionWithItems()
    {
        SetupContextMocks();
        mandate.Deliveries.Add(testDelivery);
        var collection = converter.ToStacCollection(mandate);
        Assert.IsNotNull(collection, "StacCollection should not be null.");
        Assert.AreEqual(converter.GetCollectionId(mandate), collection.Id);
        Assert.AreEqual("Test Mandate", collection.Title);
        Assert.AreEqual(string.Empty, collection.Description);
        Assert.HasCount(1, collection.Links);
        Assert.AreEqual("item", collection.Links.First().RelationshipType);
        var actualExtent = collection.Extent.Spatial.BoundingBoxes[0];
        Assert.AreEqual(mandateExtent[0], actualExtent[1]);
        Assert.AreEqual(mandateExtent[1], actualExtent[0]);
        Assert.AreEqual(mandateExtent[2], actualExtent[3]);
        Assert.AreEqual(mandateExtent[3], actualExtent[2]);
    }

    [TestMethod]
    public void ConvertToStacItem()
    {
        SetupContextMocks();
        var item = converter.ToStacItem(testDelivery);
        Assert.IsNotNull(item, "StacItem should not be null.");
        Assert.AreEqual(converter.GetItemId(testDelivery), item.Id);
        Assert.AreEqual(converter.GetCollectionId(testDelivery.Mandate), item.Collection);
        Assert.AreEqual("Datenlieferung_2023-11-06T10:45:18", item.Title);
        Assert.AreEqual(string.Empty, item.Description);
        Assert.AreNotEqual(0, item.Links.Count);

        Assert.HasCount(2, item.Assets);
        var stacAsset = item.Assets.Values.FirstOrDefault(a => a.Title == testDelivery.Assets[0].OriginalFilename);
        Assert.IsNotNull(stacAsset, "Asset with title matching original filename not found");
        Assert.AreEqual(item, stacAsset.ParentStacObject);
        Assert.AreEqual("TestFile.xtf", stacAsset.Title);
        Assert.AreEqual("PrimaryData", stacAsset.Roles.First());
        Assert.AreEqual("mocked/contenttype", stacAsset.MediaType.MediaType);
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("Lorem Ipsum", "Lorem Ipsum")]
    public void ConvertToStacItemHandelsComment(string comment, string expectedDescription)
    {
        SetupContextMocks();
        testDelivery.Comment = comment;
        var item = converter.ToStacItem(testDelivery);
        Assert.IsNotNull(item, "StacItem should not be null.");
        Assert.AreEqual(expectedDescription, item.Description);
    }

    [TestMethod]
    [DataRow(true, "Ja")]
    [DataRow(false, "Nein")]
    public void ConvertToStacItemHandlesPartialValues(bool isPartial, string? expectedPartialAttribute)
    {
        SetupContextMocks();
        testDelivery.Partial = isPartial;
        var item = converter.ToStacItem(testDelivery);
        Assert.IsNotNull(item, "StacItem should not be null.");
        var property = item.GetProperty<string>("Teillieferung");
        Assert.AreEqual(expectedPartialAttribute, property);
    }

    [TestMethod]
    public void ConvertToStacItemHandlesPartialNull()
    {
        SetupContextMocks();
        testDelivery.Partial = null;
        var item = converter.ToStacItem(testDelivery);
        Assert.IsNotNull(item, "StacItem should not be null.");
        var property = item.GetProperty<string>("Teillieferung");
        Assert.IsNull(property, "Found unexpected property Teillieferung.");
    }

    [TestMethod]
    public void ConvertToStacItemHandlesPrecusorDelivery()
    {
        SetupContextMocks();
        testDelivery.PrecursorDelivery =
            new Delivery() { Id = 25, Date = new DateTime(10, 11, 12, 13, 14, 16) };
        var item = converter.ToStacItem(testDelivery);
        Assert.IsNotNull(item, "StacItem should not be null.");
        var property = item.GetProperty<string>("Vorgängerversion");
        Assert.IsNotNull(property, "Expected property Vorgängerversion not found.");
        Assert.AreEqual("12.11.0010, 13:14:16 UTC", property);
    }

    [TestMethod]
    public void ConvertToStacItemHandlesPrecusorDeliveryNull()
    {
        SetupContextMocks();
        testDelivery.PrecursorDelivery = null;
        var item = converter.ToStacItem(testDelivery);
        Assert.IsNotNull(item, "StacItem should not be null.");
        var property = item.GetProperty<string>("Vorgängerversion");
        Assert.IsNull(property, "Found unexpected property Vorgängerversion.");
    }
}
