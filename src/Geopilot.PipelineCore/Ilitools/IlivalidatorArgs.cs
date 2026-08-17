namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Arguments forwarded to the ilivalidator tool.
/// Properties map to the corresponding ilivalidator command line options.
/// </summary>
public sealed record IlivalidatorArgs
{
    /// <summary>
    /// The INTERLIS model repositories the models and the meta configuration are resolved from, searched in the
    /// given order. Allowed entries are <c>http(s)</c> URLs and the tool placeholder <c>%ITF_DIR</c>.
    /// Setting this replaces the default of the tool entirely, so the standard repositories have to be listed
    /// explicitly if they should still apply.
    /// Maps to the ilivalidator option <c>--modeldir</c>, joined by a semicolon.
    /// </summary>
    public IReadOnlyList<string>? ModelDirs { get; init; }

    /// <summary>
    /// The validation profile to apply, in the form <c>ilidata:&lt;DatasetId&gt;</c>, resolved by the tool
    /// through <see cref="ModelDirs"/>.
    /// Maps to the ilivalidator option <c>--metaConfig</c>.
    /// </summary>
    public string? MetaConfig { get; init; }
}
