using Geopilot.Api.Models;
using Itenso.TimePeriod;
using Microsoft.AspNetCore.StaticFiles;
using NetTopologySuite.Geometries;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.WebApi.Services;
using Stac.Collection;
using System.Globalization;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Converts objects of different types to STAC objects.
/// </summary>
public class StacConverter
{
    private IStacLinker StacLinker { get; }

    private IContentTypeProvider FileContentTypeProvider { get; }

    private IStacApiContextFactory StacApiContextFactory { get; }

    private const string DeliveryNamePrefix = "Datenlieferung_";

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
    /// Returns the collection id for the specified <see cref="Mandate"/>.
    /// </summary>
    /// <param name="mandate">The <see cref="Mandate"/>.</param>
    /// <returns>Collection id.</returns>
    public string GetCollectionId(Mandate mandate)
    {
        ArgumentNullException.ThrowIfNull(mandate);

        return "coll_" + mandate.Id;
    }

    /// <summary>
    /// Returns the item id for the specified <see cref="Delivery"/>.
    /// </summary>
    /// <param name="delivery">The <see cref="Delivery"/>.</param>
    /// <returns>Item id.</returns>
    public string GetItemId(Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return "item_" + delivery.Id;
    }

    /// <summary>
    /// Converts a <see cref="Mandate"/> to a <see cref="StacCollection"/>.
    /// </summary>
    /// <param name="mandate">The <see cref="Mandate"/> to convert.</param>
    /// <returns>A STAC collection.</returns>
    public StacCollection ToStacCollection(Mandate mandate)
    {
        ArgumentNullException.ThrowIfNull(mandate);

        var collectionId = GetCollectionId(mandate);
        var items = mandate.Deliveries
            .Select(ToStacItem)
            .ToDictionary(i => i.Links.First(l => l.RelationshipType.Equals("self", StringComparison.OrdinalIgnoreCase)).Uri);

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
        ArgumentNullException.ThrowIfNull(delivery);

        if (delivery.Mandate == null)
        {
            throw new InvalidOperationException("Mandate is null for delivery " + delivery.Id);
        }

        var stacId = GetItemId(delivery);

        var item = new StacItem(stacId, ToGeoJsonPolygon(delivery.Mandate.SpatialExtent))
        {
            Collection = GetCollectionId(delivery.Mandate),
            Title = DeliveryNamePrefix + delivery.Date.ToString("s"),
            Description = delivery.Comment,
            DateTime = new TimeBlock(delivery.Date),
        };

        item.Properties.Add("Teillieferung", delivery.Partial ? "Ja" : "Nein");
        item.Properties.Add("Abgegeben durch", delivery.DeclaringUser.FullName);
        if (delivery.PrecursorDelivery != null)
        {
            item.Properties.Add("Vorgängerversion", delivery.PrecursorDelivery.Date.ToString("d.M.yyyy, H:mm:ss 'UTC'", CultureInfo.InvariantCulture));
        }

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
        ArgumentNullException.ThrowIfNull(asset);

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
        ArgumentNullException.ThrowIfNull(geometry);

        var (longMin, latMin, longMax, latMax) = GetCoordinatesBounds(geometry);
        return new GeoJSON.Net.Geometry.Polygon(new List<GeoJSON.Net.Geometry.LineString>()
        {
            new(new List<GeoJSON.Net.Geometry.Position>
            {
                new(latMin, longMin),
                new(latMin, longMax),
                new(latMax, longMax),
                new(latMax, longMin),
                new(latMin, longMin),
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
        ArgumentNullException.ThrowIfNull(geometry);

        var (longMin, latMin, longMax, latMax) = GetCoordinatesBounds(geometry);
        return new StacSpatialExtent(longMin, latMin, longMax, latMax);
    }

    private (double LongMin, double LatMin, double LongMax, double LatMax) GetCoordinatesBounds(Geometry geometry)
    {
        var coordinates = geometry.Coordinates;
        var longMin = coordinates.Min((NetTopologySuite.Geometries.Coordinate c) => c.X);
        var latMin = coordinates.Min((NetTopologySuite.Geometries.Coordinate c) => c.Y);
        var longMax = coordinates.Max((NetTopologySuite.Geometries.Coordinate c) => c.X);
        var latMax = coordinates.Max((NetTopologySuite.Geometries.Coordinate c) => c.Y);

        return (longMin, latMin, longMax, latMax);
    }
}
