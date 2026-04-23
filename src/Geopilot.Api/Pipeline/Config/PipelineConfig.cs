using Geopilot.Api.Pipeline.Config.Validation;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

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
    [YamlMember(Alias = "display_name")]
    public required Dictionary<string, string> DisplayName { get; set; }

    /// <summary>
    /// The steps in the pipeline that will be executed sequentially. Each step defines a process to execute and its data handling configuration.
    /// </summary>
    [Required(ErrorMessage = "Pipeline Step is required.")]
    [MinLength(1, ErrorMessage = "At least one Pipeline Step is required.")]
    [NoDuplicates(PropertyName = "Id")]
    [YamlMember(Alias = "steps")]
    public required List<StepConfig> Steps { get; set; }

    /// <summary>
    /// Gets or sets the list of conditions that control when delivery of the pipeline data is not allowed.
    /// If any condition evaluates to <see langword="true"/>, delivery of the pipeline data is prevented.
    /// </summary>
    /// <remarks>
    /// Each condition is evaluated at runtime and references the pipeline context data.
    /// If no conditions are defined or none evaluate to <see langword="true"/>, delivery is allowed by default.
    /// </remarks>
    [YamlMember(Alias = "delivery_restrictions")]
    public List<ConditionConfig>? DeliveryRestrictions { get; set; }
}
