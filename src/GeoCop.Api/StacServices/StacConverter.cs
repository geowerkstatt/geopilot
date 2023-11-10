using GeoCop.Api.Models;
using Itenso.TimePeriod;
using NetTopologySuite.Geometries;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.WebApi.Services;
using Stac.Collection;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Converts objects of different types to STAC objects.
    /// </summary>
    public class StacConverter
    {
        private IStacLinker StacLinker { get; }
        private FileContentTypeProvider FileContentTypeProvider { get; }
        private IStacApiContextFactory StacApiContextFactory { get; }

        /// <summary>
        /// The prefix for the collection id.
        /// </summary>
        public static readonly string CollectionIdPrefix = "coll_";

        /// <summary>
        /// The prefix for the item id.
        /// </summary>
        public static readonly string ItemnIdPrefix = "item_";

        private const string DeliveryNamePrefix = "Datenabgabe_";

        /// <summary>
        /// Initializes a new instance of the <see cref="StacConverter"/> class.
        /// </summary>
        /// <param name="stacLinker"></param>
        /// <param name="stacApiContextFactory"></param>
        /// <param name="fileContentTypeProvider"></param>
        public StacConverter(IStacLinker stacLinker, IStacApiContextFactory stacApiContextFactory, FileContentTypeProvider fileContentTypeProvider)
        {
            StacLinker = stacLinker;
            FileContentTypeProvider = fileContentTypeProvider;
            StacApiContextFactory = stacApiContextFactory;
        }

        /// <summary>
        /// Converts a <see cref="DeliveryMandate"/> to a <see cref="StacCollection"/>.
        /// </summary>
        /// <param name="mandate"></param>
        /// <returns></returns>
        public StacCollection ToStacCollection(DeliveryMandate mandate)
        {
            var collectionId = CollectionIdPrefix + mandate.Id;
            var items = mandate.Deliveries.Select(d => ToStacItem(d)).ToDictionary(i => i.Links.First(l => l.RelationshipType.ToLowerInvariant() == "self").Uri);
            if (items.Values.Count == 0)
            {
                var extent = new StacExtent(ToStacSpatialExtent(GeometryFactory.Default.CreatePolygon()), new StacTemporalExtent(DateTime.Now.ToUniversalTime(), DateTime.Now.ToUniversalTime()));
                return new StacCollection(collectionId, string.Empty, extent, null, null)
                {
                    Title = mandate.Name,
                };
            }
            else
            {
                var collection = StacCollection.Create(collectionId, string.Empty, items);
                collection.Title = mandate.Name;
                return collection;
            }
        }

        /// <summary>
        /// Converts a <see cref="Delivery"/> to a <see cref="StacItem"/>.
        /// </summary>
        /// <param name="delivery"></param>
        /// <returns></returns>
        public StacItem ToStacItem(Delivery delivery)
        {
            var stacId = ItemnIdPrefix + delivery.Id;

            var item = new StacItem(stacId, ToGeoJsonPolygon(delivery.DeliveryMandate.SpatialExtent))
            {
                Collection = CollectionIdPrefix + delivery.DeliveryMandate.Id,
                Title = DeliveryNamePrefix + delivery.Date.ToShortDateString(),
                Description = string.Empty,
                DateTime = new TimePeriodChain(),
            };
            item.DateTime.Setup(delivery.Date, delivery.Date);

            var assets = delivery.Assets.Select(file => ToStacAsset(file, item)).ToDictionary(asset => asset.Title);
            item.Assets.AddRange(assets);

            var stacApiContext = StacApiContextFactory.Create();
            StacLinker.Link(item, stacApiContext);

            return item;
        }

        /// <summary>
        /// Converts a <see cref="Asset"/> to a <see cref="StacAsset"/>.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public StacAsset ToStacAsset(Asset asset, IStacObject parent)
        {
            // TODO: Set correct Url with https://github.com/GeoWerkstatt/geocop/issues/56
            return new StacAsset(parent, new Uri("https://github.com/GeoWerkstatt/geocop/issues/56"), new List<string>() { asset.AssetType.ToString() }, asset.OriginalFilename, FileContentTypeProvider.GetContentType(asset));
        }

        /// <summary>
        /// Converts a <see cref="Geometry"/> to a <see cref="GeoJSON.Net.Geometry.Polygon"/>.
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public GeoJSON.Net.Geometry.Polygon ToGeoJsonPolygon(Geometry geometry)
        {
            var coordinates = geometry.Coordinates;
            var xMin = coordinates.Min((Coordinate c) => c.X);
            var yMin = coordinates.Min((Coordinate c) => c.Y);
            var xMax = coordinates.Max((Coordinate c) => c.X);
            var yMax = coordinates.Max((Coordinate c) => c.Y);

            var polygon = new GeoJSON.Net.Geometry.Polygon(new List<GeoJSON.Net.Geometry.LineString>()
            {
                new (new List<GeoJSON.Net.Geometry.Position>()
                {
                    new (xMin, yMin),
                    new (xMax, yMin),
                    new (xMax, yMax),
                    new (xMin, yMax),
                    new (xMin, yMin),
                }),
            });
            return polygon;
        }

        /// <summary>
        /// Converts a <see cref="Geometry"/> to a <see cref="StacSpatialExtent"/>.
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public StacSpatialExtent ToStacSpatialExtent(Geometry geometry)
        {
            var coordinates = geometry.Coordinates;
            var xMin = coordinates.Min((Coordinate c) => c.X);
            var yMin = coordinates.Min((Coordinate c) => c.Y);
            var xMax = coordinates.Max((Coordinate c) => c.X);
            var yMax = coordinates.Max((Coordinate c) => c.Y);

            return new StacSpatialExtent(xMin, yMin, xMax, yMax);
        }
    }
}
