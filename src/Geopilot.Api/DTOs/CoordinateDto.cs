using NetTopologySuite.Geometries;

namespace Geopilot.Api.DTOs;

/// <summary>
/// A coordinate in a two-dimensional space.
/// </summary>
public class CoordinateDto
{
    /// <summary>
    /// Create a new <see cref="CoordinateDto"/> from a <see cref="Coordinate"/>.
    /// </summary>
    /// <param name="coordinate"></param>
    /// <returns></returns>
    public static CoordinateDto FromCoordinate(Coordinate coordinate)
    {
        return new CoordinateDto
        {
            X = coordinate.X,
            Y = coordinate.Y,
        };
    }

    /// <summary>
    /// The x-coordinate of the coordinate.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// The y-coordinate of the coordinate.
    /// </summary>
    public double Y { get; set; }
}
