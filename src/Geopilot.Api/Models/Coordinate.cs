namespace Geopilot.Api.Models;

/// <summary>
/// A simple Coordinate representation.
/// </summary>
public struct Coordinate
{
    /// <summary>
    /// The x-coordinate.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// The y-coordinate.
    /// </summary>
    public double Y { get; set; }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        if (obj is Coordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        return false;
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        return $"({X}, {Y})";
    }

    /// <summary>
    /// Compares two <see cref="Coordinate"/> instances for equality.
    /// </summary>
    public static bool operator ==(Coordinate left, Coordinate right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two <see cref="Coordinate"/> instances for inequality.
    /// </summary>
    public static bool operator !=(Coordinate left, Coordinate right)
    {
        return !(left == right);
    }
}
