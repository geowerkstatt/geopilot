namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Arguments forwarded to the ili2gpkg tool.
/// Properties map to the corresponding ili2gpkg command line options.
/// </summary>
public sealed class Ili2GpkgArgs
{
    /// <summary>
    /// The INTERLIS models relevant to the current operation.
    /// Maps to the ili2gpkg option <c>--models</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? Models { get; set; }

    /// <summary>
    /// The default spatial reference system code (EPSG code) to use for geometries.
    /// Maps to the ili2gpkg option <c>--defaultSrsCode</c>.
    /// </summary>
    public int? DefaultSrsCode { get; set; }

    /// <summary>
    /// Disables INTERLIS validation during the import.
    /// Maps to the ili2gpkg option <c>--disableValidation</c>.
    /// </summary>
    public bool DisableValidation { get; set; }

    /// <summary>
    /// Creates a basket column in the database.
    /// Maps to the ili2gpkg option <c>--createBasketCol</c>.
    /// </summary>
    public bool CreateBasketCol { get; set; }

    /// <summary>
    /// Enables NULL constraints in the generated SQL schema.
    /// Maps to the ili2gpkg option <c>--sqlEnableNull</c>.
    /// </summary>
    public bool SqlEnableNull { get; set; }

    /// <summary>
    /// Continues the import when reference errors are encountered.
    /// Maps to the ili2gpkg option <c>--skipReferenceErrors</c>.
    /// </summary>
    public bool SkipReferenceErrors { get; set; }

    /// <summary>
    /// Continues the import when geometry errors are encountered.
    /// Maps to the ili2gpkg option <c>--skipGeometryErrors</c>.
    /// </summary>
    public bool SkipGeometryErrors { get; set; }

    /// <summary>
    /// Imports the INTERLIS TID into the database.
    /// Maps to the ili2gpkg option <c>--importTid</c>.
    /// </summary>
    public bool ImportTid { get; set; }

    /// <summary>
    /// Strokes arcs on data import.
    /// Maps to the ili2gpkg option <c>--strokeArcs</c>.
    /// </summary>
    public bool StrokeArcs { get; set; }
}
