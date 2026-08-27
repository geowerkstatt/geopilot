namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Arguments forwarded to the ilivalidator tool.
/// Properties map to the corresponding ilivalidator command line options.
/// </summary>
public sealed record IlivalidatorArgs
{
    /// <summary>
    /// The INTERLIS model repositories the models and the meta configuration are resolved from, searched in the
    /// given order. Allowed entries are <c>http(s)</c> URLs and the tool placeholder <c>%ITF_DIR</c>, alone or with
    /// a relative subfolder: the service keeps delivered model files in <c>%ITF_DIR/models</c> and an extracted
    /// repository archive in <c>%ITF_DIR/repository</c>, each visible only through its own entry.
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

    /// <summary>
    /// Whether the validator may assume that every object it needs is contained in the validated file. A reference
    /// to an object outside it is then an error instead of a check the validator skips.
    /// Maps to the ilivalidator option <c>--allObjectsAccessible</c>, a switch without a counterpart: setting this
    /// can only add the behaviour, never remove it. It takes effect as soon as either this or the meta configuration
    /// asks for it, so <see langword="false"/> leaves the decision to the meta configuration.
    /// </summary>
    public bool AllObjectsAccessible { get; init; }

    /// <summary>
    /// The plugins that provide the user defined functions a model may call in its constraints, selected by the id
    /// the ilitools-wrapper offers them under. Which ids exist is a property of that deployment, and an id it does
    /// not offer is rejected before the tool runs.
    /// Unlike the other properties this is not a 1:1 mapping: the tool option <c>--plugins</c> takes a single
    /// folder, which the wrapper composes from this selection.
    /// </summary>
    /// <remarks>
    /// A constraint whose function has no implementation is skipped with a warning while the run still reports
    /// success, so a missing plugin does not surface in <see cref="IlivalidatorResult.Success"/>.
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
}
