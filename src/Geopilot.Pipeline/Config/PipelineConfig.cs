using Geopilot.Pipeline.ValidationAttributes;
using Geopilot.PipelineCore.Pipeline;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Configuration for a pipeline.
/// </summary>
[ValidStepInputReference]
[ValidExpressionParameterReferences]
public class PipelineConfig
{
    /// <summary>
    /// The pipeline identifier. A pipeline is uniquely identified by its id.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Pipeline ID is required.")]
    [YamlMember(Alias = "id")]
    public required string Id { get; set; }

    /// <summary>
    /// The pipelines display name. A human-readable name for the pipeline.
    /// </summary>
    [Required(ErrorMessage = "Pipeline Display Name is required.")]
    [YamlMember(Alias = "display_name")]
    public required LocalizedText DisplayName { get; set; }

    /// <summary>
    /// The steps in the pipeline that will be executed sequentially. Each step defines a process to execute and its data handling configuration.
    /// </summary>
    [Required(ErrorMessage = "Pipeline Step is required.")]
    [MinLength(1, ErrorMessage = "At least one Pipeline Step is required.")]
    [NoDuplicates(PropertyName = "Id")]
    [YamlMember(Alias = "steps")]
    public required List<StepConfig> Steps { get; set; }
}
