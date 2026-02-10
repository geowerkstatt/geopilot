namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents a set of parameters for pipeline processing.
/// </summary>
public class Parameterization : Dictionary<string, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Parameterization"/> class.
    /// </summary>
    public Parameterization()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameterization"/> class.
    /// </summary>
    public Parameterization(Parameterization src)
        : base(src)
    {
    }
}
