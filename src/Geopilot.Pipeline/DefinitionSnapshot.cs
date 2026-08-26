using Geopilot.Pipeline.Config;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline;

/// <summary>
/// The definition snapshot of a single pipeline, shaped like a minimal pipeline definition file:
/// the pipeline as configured, the process catalog entries its steps reference, and the effective
/// base configuration of those implementations. Serialized to JSON by
/// <see cref="PipelineFactory.GetDefinitionSnapshotJson"/> and persisted per run by the host.
/// </summary>
internal sealed class DefinitionSnapshot
{
    /// <summary>
    /// The single pipeline the snapshot was taken for. A list so the document reads like a definition file.
    /// </summary>
    [YamlMember(Alias = "pipelines")]
    public required List<PipelineConfig> Pipelines { get; set; }

    /// <summary>
    /// The process catalog entries referenced by the pipeline's steps.
    /// </summary>
    [YamlMember(Alias = "processes")]
    public required List<ProcessConfig> Processes { get; set; }

    /// <summary>
    /// The base configuration (appsettings <c>Pipeline:ProcessConfigs</c>) effective for the referenced
    /// implementations. Not part of the definition file, but it decides behavior (e.g. which model
    /// repositories a validation checks against), so the snapshot has to carry it. Omitted when empty.
    /// </summary>
    [YamlMember(Alias = "process_configs")]
    public Dictionary<string, Parameterization>? ProcessConfigs { get; set; }
}
