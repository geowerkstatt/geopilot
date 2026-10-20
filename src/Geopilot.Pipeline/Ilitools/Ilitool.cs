namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// The tool of the ilitools-wrapper that wrote a log. It decides how an unresolved meta configuration is worded,
/// and lets a message name the tool and the process setting to fix.
/// </summary>
internal enum Ilitool
{
    /// <summary>
    /// ilivalidator, run by the XTF validation.
    /// </summary>
    Ilivalidator,

    /// <summary>
    /// ili2gpkg, run by the GeoPackage processes.
    /// </summary>
    Ili2gpkg,
}
