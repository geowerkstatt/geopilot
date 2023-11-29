using GeoCop.Api.Models;
using Itenso.TimePeriod;
using Microsoft.AspNetCore.StaticFiles;
using NetTopologySuite.Geometries;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.WebApi.Services;
using Stac.Collection;

namespace GeoCop.Api.StacServices;

/// <summary>
/// Converts objects of different types to STAC objects.
/// </summary>
public class StacConverter
{
    private IStacLinker StacLinker { get; }
    private IContentTypeProvider FileContentTypeProvider { get; }
    private IStacApiContextFactory StacApiContextFactory { get; }

    private const string DeliveryNamePrefix = "Datenabgabe_";

    /// <summary>
    /// Initializes a new instance of the <see cref="StacConverter"/> class.
    /// </summary>
    public StacConverter(IStacLinker stacLinker, IStacApiContextFactory stacApiContextFactory, IContentTypeProvider fileContentTypeProvider)
    {
        StacLinker = stacLinker;
        FileContentTypeProvider = fileContentTypeProvider;
        StacApiContextFactory = stacApiContextFactory;
    }

    /// <summary>
    /// Returns the collection id for the specified <see cref="DeliveryMandate"/>.
    /// </summary>
    /// <param name="mandate">The <see cref="DeliveryMandate"/>.</param>
    /// <returns>Collection id.</returns>
    public string GetCollectionId(DeliveryMandate mandate) => "coll_" + mandate.Id;

    /// <summary>
    /// Returns the item id for the specified <see cref="Delivery"/>.
    /// </summary>
    /// <param name="delivery">The <see cref="Delivery"/>.</param>
    /// <returns>Item id.</returns>
    public string GetItemId(Delivery delivery) => "item_" + delivery.Id;

    /// <summary>
    /// Converts a <see cref="DeliveryMandate"/> to a <see cref="StacCollection"/>.
    /// </summary>
    /// <param name="mandate">The <see cref="DeliveryMandate"/> to convert.</param>
    /// <returns>A STAC collection.</returns>
    public StacCollection ToStacCollection(DeliveryMandate mandate)
    {
        var collectionId = GetCollectionId(mandate);
        var items = mandate.Deliveries
            .Select(ToStacItem)
            .ToDictionary(i => i.Links.First(l => l.RelationshipType.ToLowerInvariant() == "self").Uri);

        if (items.Values.Count == 0)
        {
            var nowTimestamp = DateTime.Now.ToUniversalTime();
            var extent = new StacExtent(ToStacSpatialExtent(mandate.SpatialExtent), new StacTemporalExtent(nowTimestamp, nowTimestamp));
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
    /// <param name="delivery">The <see cref="Delivery"/> to convert.</param>
    /// <returns>The STAC item.</returns>
    public StacItem ToStacItem(Delivery delivery)
    {
        var stacId = GetItemId(delivery);

        var item = new StacItem(stacId, ToGeoJsonPolygon(delivery.DeliveryMandate.SpatialExtent))
        {
            Collection = GetCollectionId(delivery.DeliveryMandate),
            Title = DeliveryNamePrefix + delivery.Date.ToString("s"),
            Description = string.Empty,
            DateTime = new TimePeriodChain(),
        };
        item.DateTime.Setup(delivery.Date, delivery.Date);

        var stacApiContext = StacApiContextFactory.Create();
        var assets = delivery.Assets.Select(file => ToStacAsset(file, item, stacApiContext.BaseUri)).ToDictionary(asset => asset.Title);
        item.Assets.AddRange(assets);
        StacLinker.Link(item, stacApiContext);

        return item;
    }

    /// <summary>
    /// Converts a <see cref="Asset"/> to a <see cref="StacAsset"/>.
    /// </summary>
    /// <param name="asset">The <see cref="Asset"/> to convert.</param>
    /// <param name="parent">The parent <see cref="IStacObject"/> to which the asset belongs.</param>
    /// <param name="baseUri">The baseUri to build the download link.</param>
    /// <returns>The STAC asset.</returns>
    public StacAsset ToStacAsset(Asset asset, IStacObject parent, Uri baseUri)
    {
        var downloadLink = new Uri(baseUri, "api/v1/delivery/assets/" + asset.Id);
        return new StacAsset(parent, downloadLink, new List<string>() { asset.AssetType.ToString() }, asset.OriginalFilename, FileContentTypeProvider.GetContentType(asset));
    }

    /// <summary>
    /// Converts a <see cref="Geometry"/> to a <see cref="GeoJSON.Net.Geometry.Polygon"/>.
    /// </summary>
    /// <param name="geometry">The <see cref="Geometry"/>.</param>
    /// <returns>The <see cref="GeoJSON.Net.Geometry.Polygon"/>.</returns>
    public GeoJSON.Net.Geometry.Polygon ToGeoJsonPolygon(Geometry geometry)
    {
        var (xMin, yMin, xMax, yMax) = GetCoordinatesBounds(geometry);
        return new GeoJSON.Net.Geometry.Polygon(new List<GeoJSON.Net.Geometry.LineString>()
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
    }

    /// <summary>
    /// Converts a <see cref="Geometry"/> to a <see cref="StacSpatialExtent"/> (bounding box of a StacObject).
    /// </summary>
    /// <param name="geometry">The <see cref="Geometry"/>.</param>
    /// <returns>The <see cref="StacSpatialExtent"/>.</returns>
    public StacSpatialExtent ToStacSpatialExtent(Geometry geometry)
    {
        var (xMin, yMin, xMax, yMax) = GetCoordinatesBounds(geometry);
        return new StacSpatialExtent(xMin, yMin, xMax, yMax);
    }

    private (double xMin, double yMin, double xMax, double yMax) GetCoordinatesBounds(Geometry geometry)
    {
        var coordinates = geometry.Coordinates;
        var xMin = coordinates.Min((Coordinate c) => c.X);
        var yMin = coordinates.Min((Coordinate c) => c.Y);
        var xMax = coordinates.Max((Coordinate c) => c.X);
        var yMax = coordinates.Max((Coordinate c) => c.Y);

        return (xMin, yMin, xMax, yMax);
    }
}
