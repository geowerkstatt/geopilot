using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
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
    /// Indicates whether the mandate is public or restricted.
    /// Public mandates can be accessed by anyone, even not logged in users.
    /// Non-public, restricted mandates can only be accessed by logged in users that are part of an organisation listed in the mandates organisations.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether delivery is permitted.
    /// </summary>
    public bool AllowDelivery { get; set; }

    /// <summary>
    /// List of file types that are allowed to be delivered. Include the period "." and support wildcards "*".
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public string[] FileTypes { get; set; } = Array.Empty<string>();
#pragma warning restore CA1819 // Properties should not return arrays

    /// <summary>
    /// ID of the INTERLIS validation profile to be used when validating INTERLIS data.
    /// </summary>
    public string? InterlisValidationProfile { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the pipeline associated with this instance.
    /// </summary>
    [Column(TypeName = "varchar(128)")]
    public string? PipelineId { get; set; }

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
    /// Defines how <see cref="Delivery.PrecursorDelivery"/> is evaluated.
    /// </summary>
    [Column(TypeName = "varchar(12)")]
    public FieldEvaluationType EvaluatePrecursorDelivery { get; set; }

    /// <summary>
    /// Defines how <see cref="Delivery.Partial"/> is evaluated.
    /// </summary>
    [Column(TypeName = "varchar(12)")]
    [AllowedValues(FieldEvaluationType.NotEvaluated, FieldEvaluationType.Required)]
    public FieldEvaluationType EvaluatePartial { get; set; }

    /// <summary>
    /// Defines how <see cref="Delivery.Comment"/> is evaluated.
    /// </summary>
    [Column(TypeName = "varchar(12)")]
    public FieldEvaluationType EvaluateComment { get; set; }

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
    /// <returns><c>true</c> if this mandate has 2 coordinates to create the spatial extent; otherwise, <c>false</c>.</returns>
    public bool SetPolygonFromCoordinates()
    {
        if (Coordinates.Count != 2)
        {
            return false;
        }

        SpatialExtent = Geometry.DefaultFactory.CreatePolygon(new NetTopologySuite.Geometries.Coordinate[]
        {
            new(Coordinates[0].X, Coordinates[0].Y),
            new(Coordinates[0].X, Coordinates[1].Y),
            new(Coordinates[1].X, Coordinates[1].Y),
            new(Coordinates[1].X, Coordinates[0].Y),
            new(Coordinates[0].X, Coordinates[0].Y),
        });
        return true;
    }

    /// <summary>
    /// Transforms the <see cref="SpatialExtent"/> polygon to a <see cref="Coordinates"/> list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Exception thrown if the coordinate list is of an invalid length.</exception>
    public void SetCoordinateListFromPolygon()
    {
        Coordinates = new List<Coordinate>();

        switch (SpatialExtent.Coordinates.Length)
        {
            case 0:
                break;
            case 5:
                double minX = SpatialExtent.Coordinates.Min(coord => coord.X);
                double minY = SpatialExtent.Coordinates.Min(coord => coord.Y);
                double maxX = SpatialExtent.Coordinates.Max(coord => coord.X);
                double maxY = SpatialExtent.Coordinates.Max(coord => coord.Y);

                Coordinates.Add(new Coordinate { X = minX, Y = minY });
                Coordinates.Add(new Coordinate { X = maxX, Y = maxY });
                break;
            default:
                throw new InvalidOperationException("Unsupported number of coordinates. Spatial extent must be a rectangle.");
        }
    }
}
