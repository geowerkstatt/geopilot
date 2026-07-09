namespace Geopilot.Pipeline.Config;

/// <summary>
/// Represents a set of parameters for pipeline processing.
/// Supports scalar values, lists and nested objects.
/// </summary>
public class Parameterization : Dictionary<string, object?>
{
    /// <summary>
    /// Initializes a new instance of the Parameterization class.
    /// </summary>
    public Parameterization()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Parameterization class by copying the values from the specified source
    /// instance.
    /// </summary>
    /// <remarks>This constructor creates a deep copy of the provided source instance, ensuring that the new
    /// instance has the same state as the original.</remarks>
    /// <param name="src">The source Parameterization instance from which to copy values. This parameter cannot be null.</param>
    public Parameterization(Parameterization src)
        : base(src)
    {
    }
}
