using GeoCop.Api.Models;
using GeoCop.Api.StacServices;
using Moq;
using NetTopologySuite.Geometries;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.Models;
using Stac.Api.WebApi.Services;

namespace GeoCop.Api.Test
{
    [TestClass]
    public class StacConverterTest
    {
        private StacConverter converter;

        private Delivery testDelivery = new ()
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
        private DeliveryMandate mandate = new ()
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

        [TestInitialize]
        public void Initialize()
        {
            var linkerMoq = new Mock<IStacLinker>();
            linkerMoq.Setup(linker => linker.Link(It.IsAny<StacItem>(), It.IsAny<IStacApiContext>()))
                .Callback((StacItem item, IStacApiContext context) =>
                {
                    item.Links.Add(new StacApiLink(
                        new Uri("https://iamalink.com"),
                        "self",
                        "Title",
                        "application/octet-stream"));
                    });
            var contextFactory = new Mock<IStacApiContextFactory>().Object;
            converter = new StacConverter(linkerMoq.Object, contextFactory, new FileContentTypeProvider());
        }

        [TestCleanup]
        public void Cleanup()
        {
            mandate.Deliveries.Clear();
        }

        [TestMethod]
        public void ConvertToStacCollectionWithoutItems()
        {
            var collection = converter.ToStacCollection(mandate);
            Assert.IsNotNull(collection);
            Assert.AreEqual(StacConverter.CollectionIdPrefix + mandate.Id, collection.Id);
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
            mandate.Deliveries.Add(testDelivery);
            var collection = converter.ToStacCollection(mandate);
            Assert.IsNotNull(collection);
            Assert.AreEqual(StacConverter.CollectionIdPrefix + mandate.Id, collection.Id);
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
            var item = converter.ToStacItem(testDelivery);
            Assert.IsNotNull(item);
            Assert.AreEqual(StacConverter.ItemIdPrefix + testDelivery.Id, item.Id);
            Assert.AreEqual(StacConverter.CollectionIdPrefix + testDelivery.DeliveryMandate.Id, item.Collection);
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
