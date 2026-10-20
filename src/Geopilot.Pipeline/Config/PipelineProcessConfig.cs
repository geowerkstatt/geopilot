using Geopilot.Pipeline.Validation;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Represents the configuration for pipeline processes, including the collection of process and pipeline definitions.
/// The steps in <see cref="PipelineConfig"/> reference to the <see cref="ProcessConfig"/> by its name."/>.
/// </summary>
[ValidProcessReference]
[NoUndefinedBaseConfigOverwrite]
[NoBaseConfigOverwrite]
public class PipelineProcessConfig
{
    /// <summary>
    /// List of process configurations available for use in pipelines.
    /// </summary>
    [YamlMember(Alias = "processes")]
    [Required(ErrorMessage = "Processes are required.")]
    [MinLength(1, ErrorMessage = "At least one Process is required.")]
    [NoDuplicates(PropertyName = "Id")]
    public required List<ProcessConfig> Processes { get; set; }

    /// <summary>
    /// List of pipeline configurations defining various pipelines. A Pipeline is uniquely identified by its name.
    /// </summary>
    [YamlMember(Alias = "pipelines")]
    [Required(ErrorMessage = "Pipelines are required.")]
    [MinLength(1, ErrorMessage = "At least one Pipeline is required.")]
    [NoDuplicates(PropertyName = "Id")]
    public required List<PipelineConfig> Pipelines { get; set; }

    /// <summary>
    /// Named sets of reactions that run once a delivery can no longer be withdrawn. Optional: without them, no
    /// mandate can choose a set.
    /// </summary>
    [YamlMember(Alias = "delivery_processing")]
    [NoDuplicates(PropertyName = "Id")]
    [UniqueReactionIds]
    public List<DeliveryProcessingConfig>? DeliveryProcessing { get; set; }
}
