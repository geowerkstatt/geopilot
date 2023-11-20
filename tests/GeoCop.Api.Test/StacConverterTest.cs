using GeoCop.Api.Models;
using Microsoft.AspNetCore.StaticFiles;
using Moq;
using NetTopologySuite.Geometries;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.Models;
using Stac.Api.WebApi.Services;

namespace GeoCop.Api.StacServices
{
    [TestClass]
    public class StacConverterTest
    {
        private readonly Delivery testDelivery = new ()
        {
            Id = 1,
            Date = new DateTime(2023, 11, 6, 10, 45, 18),
            DeclaringUser = new User()
            {
                Id = 2,
            },
            DeliveryMandate = new DeliveryMandate()
            {
                Id = 3,
                SpatialExtent = new Polygon(new LinearRing(new Coordinate[]
            {
                        new (1, 1),
                        new (3, 1),
                        new (3, 3),
                        new (1, 3),
                        new (1, 1),
            })),
            },
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
        private readonly DeliveryMandate mandate = new ()
        {
            Id = 1,
            Name = "Test Mandate",
            SpatialExtent = new Polygon(new LinearRing(new Coordinate[]
            {
                    new (10, 10),
                    new (30, 10),
                    new (30, 30),
                    new (10, 30),
                    new (10, 10),
            })),
        };

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
        }

        [TestCleanup]
        public void Cleanup()
        {
            contentTypeProviderMock.VerifyAll();
            contextFactoryMock.VerifyAll();
            stacLinkerMock.VerifyAll();
            mandate.Deliveries.Clear();
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
            Assert.IsNotNull(collection);
            Assert.AreEqual(converter.GetCollectionId(mandate), collection.Id);
            Assert.AreEqual("Test Mandate", collection.Title);
            Assert.AreEqual(string.Empty, collection.Description);
            Assert.AreEqual(0, collection.Links.Count);
            var expectedExtent = converter.ToStacSpatialExtent(mandate.SpatialExtent).BoundingBoxes[0];
            var actualExtent = collection.Extent.Spatial.BoundingBoxes[0];
            Assert.IsTrue(Enumerable.Range(0, expectedExtent.GetLength(0))
                .All(i => expectedExtent[i] == actualExtent[i]));
        }

        [TestMethod]
        public void ConvertToStacCollectionWithItems()
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
            contextFactoryMock.Setup(factory => factory.Create()).Returns(contextMock.Object);
            var contentType = "text/plain";
            contentTypeProviderMock.Setup(x => x.TryGetContentType(It.IsAny<string>(), out contentType)).Returns(true);
            mandate.Deliveries.Add(testDelivery);
            var collection = converter.ToStacCollection(mandate);
            Assert.IsNotNull(collection);
            Assert.AreEqual(converter.GetCollectionId(mandate), collection.Id);
            Assert.AreEqual("Test Mandate", collection.Title);
            Assert.AreEqual(string.Empty, collection.Description);
            Assert.AreEqual(1, collection.Links.Count);
            Assert.AreEqual("item", collection.Links.First().RelationshipType);
            var expectedExtent = converter.ToStacSpatialExtent(testDelivery.DeliveryMandate.SpatialExtent).BoundingBoxes[0];
            var actualExtent = collection.Extent.Spatial.BoundingBoxes[0];
            Assert.IsTrue(Enumerable.Range(0, expectedExtent.GetLength(0))
                .All(i => expectedExtent[i] == actualExtent[i]));
        }

        [TestMethod]
        public void ConvertToStacItem()
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
            contextFactoryMock.Setup(factory => factory.Create()).Returns(contextMock.Object);
            var contentType = "application/interlis+xml";
            contentTypeProviderMock.Setup(x => x.TryGetContentType(It.IsAny<string>(), out contentType)).Returns(true);
            var item = converter.ToStacItem(testDelivery);
            Assert.IsNotNull(item);
            Assert.AreEqual(converter.GetItemId(testDelivery), item.Id);
            Assert.AreEqual(converter.GetCollectionId(testDelivery.DeliveryMandate), item.Collection);
            Assert.AreEqual("Datenabgabe_2023-11-06T10:45:18", item.Title);
            Assert.AreEqual(string.Empty, item.Description);
            Assert.IsTrue(item.Links.Any());

            Assert.AreEqual(2, item.Assets.Count);
            var stacAsset = item.Assets[testDelivery.Assets[0].OriginalFilename];
            Assert.AreEqual(item, stacAsset.ParentStacObject);
            Assert.AreEqual("TestFile.xtf", stacAsset.Title);
            Assert.AreEqual("PrimaryData", stacAsset.Roles.First());
            Assert.AreEqual("application/interlis+xml", stacAsset.MediaType.MediaType);
        }
    }
}
