using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Models;

/// <summary>
/// A contract between the system owner and an organisation for data delivery.
/// The mandate describes where and in what format data should be delivered.
/// </summary>
public class Mandate
{
    /// <summary>
    /// The unique identifier for the mandate.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The display name of the mandate.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of file types that are allowed to be delivered. Include the period "." and support wildcards "*".
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public string[] FileTypes { get; set; } = Array.Empty<string>();
#pragma warning restore CA1819 // Properties should not return arrays

    /// <summary>
    /// The spatial extent of the mandate. The extent is a polygon in WGS84.
    /// Delivered data must be within the extent.
    /// </summary>
    [JsonIgnore]
    public Geometry SpatialExtent { get; set; } = GeometryFactory.Default.CreatePolygon();

    /// <summary>
    /// The coordinates representing the spatial extent of the mandate.
    /// </summary>
    [NotMapped]
    public List<Coordinate> Coordinates { get; set; } = new List<Coordinate>();

    /// <summary>
    /// Organisations allowed to deliver data fulfilling the mandate.
    /// </summary>
    public List<Organisation> Organisations { get; set; } = new List<Organisation>();

    /// <summary>
    /// Data deliveries that have been declared fulfilling the mandate.
    /// </summary>
    public List<Delivery> Deliveries { get; set; } = new List<Delivery>();

    /// <summary>
    /// Transforms the <see cref="Coordinates"/> list to a <see cref="SpatialExtent"/> polygon.
    /// </summary>
    public void SetPolygonFromCoordinates()
    {
        if (Coordinates.Count != 2)
        {
            SpatialExtent = Geometry.DefaultFactory.CreatePolygon();
        }
        else
        {
            SpatialExtent = Geometry.DefaultFactory.CreatePolygon(new NetTopologySuite.Geometries.Coordinate[]
            {
                new (Coordinates[0].X, Coordinates[0].Y),
                new (Coordinates[0].X, Coordinates[1].Y),
                new (Coordinates[1].X, Coordinates[1].Y),
                new (Coordinates[1].X, Coordinates[0].Y),
                new (Coordinates[0].X, Coordinates[0].Y),
            });
        }
    }

    /// <summary>
    /// Transforms the <see cref="SpatialExtent"/> polygon to a <see cref="Coordinates"/> list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Exception thrown if the coordinate list is of an invalid length</exception>
    public void SetCoordinateListFromPolygon()
    {
        Coordinates = new List<Coordinate>();

        switch (SpatialExtent.Coordinates.Length)
        {
            case 0:
                break;
            case 5:
                double minX = SpatialExtent.Coordinates[0].X;
                double minY = SpatialExtent.Coordinates[0].Y;
                double maxX = SpatialExtent.Coordinates[0].X;
                double maxY = SpatialExtent.Coordinates[0].Y;

                foreach (var coord in SpatialExtent.Coordinates)
                {
                    minX = Math.Min(minX, coord.X);
                    minY = Math.Min(minY, coord.Y);
                    maxX = Math.Max(maxX, coord.X);
                    maxY = Math.Max(maxY, coord.Y);
                }

                Coordinates.Add(new Coordinate { X = minX, Y = minY });
                Coordinates.Add(new Coordinate { X = maxX, Y = maxY });
                break;
            default:
                throw new InvalidOperationException($"Unsupported number of coordinates. Spatial extent must be a rectangle.");
        }
    }
}
