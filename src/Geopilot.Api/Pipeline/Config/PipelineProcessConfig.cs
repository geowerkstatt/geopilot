using System.Diagnostics;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for pipeline processes, including the collection of process and pipeline definitions.
/// The steps in <see cref="PipelineConfig"/> reference to the <see cref="ProcessConfig"/> by its name."/>.
/// </summary>
internal class PipelineProcessConfig
{
    /// <summary>
    /// List of process configurations available for use in pipelines.
    /// </summary>
    [YamlMember(Alias = "processes")]
    public required List<ProcessConfig> Processes { get; set; }

    /// <summary>
    /// List of pipeline configurations defining various pipelines. A Pipeline is uniquely identified by its name.
    /// </summary>
    [YamlMember(Alias = "pipelines")]
    public required List<PipelineConfig> Pipelines { get; set; }
}
