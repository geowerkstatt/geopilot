namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Arguments forwarded to the ili2gpkg tool.
/// Properties map to the corresponding ili2gpkg command line options.
/// </summary>
public sealed record Ili2GpkgArgs
{
    /// <summary>
    /// The INTERLIS models relevant to the current operation.
    /// Maps to the ili2gpkg option <c>--models</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? Models { get; init; }

    /// <summary>
    /// The INTERLIS model repositories the models are resolved from, searched in the given order.
    /// Allowed entries are <c>http(s)</c> URLs and the tool placeholders <c>%XTF_DIR</c> and <c>%ILI_FROM_DB</c>.
    /// Setting this replaces the default of the tool entirely, so the standard repositories have to be listed
    /// explicitly if they should still apply.
    /// Maps to the ili2gpkg option <c>--modeldir</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? ModelDirs { get; init; }

    /// <summary>
    /// The meta configuration to apply, in the form <c>ilidata:&lt;DatasetId&gt;</c>, resolved by the tool
    /// through <see cref="ModelDirs"/>.
    /// Maps to the ili2gpkg option <c>--metaConfig</c>.
    /// </summary>
    public string? MetaConfig { get; init; }

    /// <summary>
    /// The default spatial reference system code (EPSG code) to use for geometries.
    /// Maps to the ili2gpkg option <c>--defaultSrsCode</c>.
    /// </summary>
    public int? DefaultSrsCode { get; init; }

    /// <summary>
    /// Disables INTERLIS validation during the import.
    /// Maps to the ili2gpkg option <c>--disableValidation</c>.
    /// </summary>
    public bool DisableValidation { get; init; }

    /// <summary>
    /// Creates a basket column in the database.
    /// Maps to the ili2gpkg option <c>--createBasketCol</c>.
    /// </summary>
    public bool CreateBasketCol { get; init; }

    /// <summary>
    /// Enables NULL constraints in the generated SQL schema.
    /// Maps to the ili2gpkg option <c>--sqlEnableNull</c>.
    /// </summary>
    public bool SqlEnableNull { get; init; }

    /// <summary>
    /// Continues the import when reference errors are encountered.
    /// Maps to the ili2gpkg option <c>--skipReferenceErrors</c>.
    /// </summary>
    public bool SkipReferenceErrors { get; init; }

    /// <summary>
    /// Continues the import when geometry errors are encountered.
    /// Maps to the ili2gpkg option <c>--skipGeometryErrors</c>.
    /// </summary>
    public bool SkipGeometryErrors { get; init; }

    /// <summary>
    /// Imports the INTERLIS TID into the database.
    /// Maps to the ili2gpkg option <c>--importTid</c>.
    /// </summary>
    public bool ImportTid { get; init; }

    /// <summary>
    /// Strokes arcs on data import.
    /// Maps to the ili2gpkg option <c>--strokeArcs</c>.
    /// </summary>
    public bool StrokeArcs { get; init; }

    /// <summary>
    /// The dataset to use for the operation.
    /// Maps to the ili2gpkg option <c>--dataset</c>.
    /// </summary>
    public string? Dataset { get; init; }
}
