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
    /// Allowed entries are <c>http(s)</c> URLs and the tool placeholders <c>%XTF_DIR</c> and <c>%ILI_FROM_DB</c>;
    /// <c>%XTF_DIR</c> also with a relative subfolder: the service keeps delivered model files in
    /// <c>%XTF_DIR/models</c> and an extracted repository archive in <c>%XTF_DIR/repository</c>.
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

    /// <summary>
    /// The plugins that provide the user defined functions a model may call in its constraints, selected by the id
    /// the ilitools-wrapper offers them under. Which ids exist is a property of that deployment, and an id it does
    /// not offer is rejected before the tool runs.
    /// Unlike the other properties this is not a 1:1 mapping: the tool option <c>--plugins</c> takes a single
    /// folder, which the wrapper composes from this selection.
    /// </summary>
    /// <remarks>
    /// Relevant wherever the operation validates, which <c>ili2gpkg</c> does on import and through
    /// <c>--validate</c>. A constraint whose function has no implementation is skipped with a warning while the
    /// run still reports success, so a missing plugin does not surface in the result.
    /// </remarks>
    public IReadOnlyList<string>? PluginIds { get; init; }

    /// <summary>
    /// The version of the tool to run, selected from the versions the ilitools-wrapper offers. Which versions exist
    /// is a property of that deployment, and a version it does not offer is rejected before the tool runs.
    /// Leaving this unset runs the version the deployment configured as its default, which is deliberately
    /// decoupled from the newest version it offers, so a new version can be offered without taking effect on its own.
    /// </summary>
    /// <remarks>
    /// The version selects the tool installation rather than a tool option, so it appears in no command line. Which
    /// version actually ran is visible in the log header the tool writes.
    /// </remarks>
    public string? ToolVersion { get; init; }

    /// <summary>
    /// Creates foreign key constraints for columns that reference records in other tables.
    /// Maps to the ili2gpkg option <c>--createFk</c>.
    /// </summary>
    public bool CreateFk { get; init; }

    /// <summary>
    /// Creates a spatial index for each geometry column.
    /// Maps to the ili2gpkg option <c>--createGeomIdx</c>.
    /// </summary>
    public bool CreateGeomIdx { get; init; }

    /// <summary>
    /// Creates a table with the enumeration values for each enumeration definition.
    /// Maps to the ili2gpkg option <c>--createEnumTabs</c>.
    /// </summary>
    public bool CreateEnumTabs { get; init; }

    /// <summary>
    /// Creates an additional column <c>T_Ili_Tid</c> in each table.
    /// Maps to the ili2gpkg option <c>--createTidCol</c>.
    /// </summary>
    public bool CreateTidCol { get; init; }

    /// <summary>
    /// Fills the enumeration display name with the XTF code without underscores.
    /// Maps to the ili2gpkg option <c>--beautifyEnumDispName</c>.
    /// </summary>
    public bool BeautifyEnumDispName { get; init; }

    /// <summary>
    /// Maps the inheritance hierarchy with the dynamic smart1 strategy.
    /// Maps to the ili2gpkg option <c>--smart1Inheritance</c>.
    /// </summary>
    public bool Smart1Inheritance { get; init; }

    /// <summary>
    /// Maps the inheritance hierarchy with the dynamic smart2 strategy.
    /// Maps to the ili2gpkg option <c>--smart2Inheritance</c>.
    /// </summary>
    public bool Smart2Inheritance { get; init; }

    /// <summary>
    /// Maps all simple INTERLIS attributes to TEXT columns, so that invalid data can be imported.
    /// Maps to the ili2gpkg option <c>--sqlColsAsText</c>.
    /// </summary>
    public bool SqlColsAsText { get; init; }

    /// <summary>
    /// Creates the tables during the data import, so no separate schema import is required.
    /// Maps to the ili2gpkg option <c>--doSchemaImport</c>.
    /// </summary>
    public bool DoSchemaImport { get; init; }

    /// <summary>
    /// Deletes all data in the existing tables before the data import, keeping the table structure.
    /// Maps to the ili2gpkg option <c>--deleteData</c>.
    /// </summary>
    public bool DeleteData { get; init; }

    /// <summary>
    /// Reads the line tables of ITF files as they are, without building polygons.
    /// Maps to the ili2gpkg option <c>--skipPolygonBuilding</c>.
    /// </summary>
    public bool SkipPolygonBuilding { get; init; }

    /// <summary>
    /// Imports the basket identification (BID) into the column <c>T_Ili_Tid</c> of the table <c>t_ili2db_basket</c>.
    /// Maps to the ili2gpkg option <c>--importBid</c>.
    /// </summary>
    public bool ImportBid { get; init; }

    /// <summary>
    /// The qualified names (<c>Model.Topic</c>) of the topics whose baskets are imported, exported or validated.
    /// Maps to the ili2gpkg option <c>--topics</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? Topics { get; init; }

    /// <summary>
    /// The BIDs of the baskets to import, export or validate.
    /// Maps to the ili2gpkg option <c>--baskets</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? Baskets { get; init; }

    /// <summary>
    /// The base or translated models the data is exported or validated against, instead of the models it was captured in.
    /// Maps to the ili2gpkg option <c>--exportModels</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? ExportModels { get; init; }

    /// <summary>
    /// The format of the transfer file(s). Without a value, the xtf format is used.
    /// For an INTERLIS 1 transfer file (itf), the transfer format must be set to <see cref="Ili2GpkgTransferFormat.Itf"/>,
    /// as the wrapper ignores the file extension of the transfer file.
    /// <see cref="Ili2GpkgTransferFormat.Gml"/> and <see cref="Ili2GpkgTransferFormat.Gml20"/> are only accepted for export.
    /// </summary>
    public Ili2GpkgTransferFormat? TransferFormat { get; init; }

    /// <summary>
    /// Disables the validation of the AREA topology.
    /// Maps to the ili2gpkg option <c>--disableAreaValidation</c>.
    /// </summary>
    public bool DisableAreaValidation { get; init; }

    /// <summary>
    /// Restricts relaxing the validation through a validation configuration to <c>multiplicity</c>.
    /// Maps to the ili2gpkg option <c>--forceTypeValidation</c>.
    /// </summary>
    public bool ForceTypeValidation { get; init; }

    /// <summary>
    /// The validation configuration to apply, in the form <c>ilidata:&lt;DatasetId&gt;</c>, resolved by the tool
    /// through <see cref="ModelDirs"/>.
    /// Maps to the ili2gpkg option <c>--validConfig</c>.
    /// </summary>
    public string? ValidConfig { get; init; }
}
