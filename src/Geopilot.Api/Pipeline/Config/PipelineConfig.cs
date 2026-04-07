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
    /// Condition to control in which cases a delivery is allowed and not allowed, based on the results of the pipeline run.
    /// </summary>
    /// <remarks>
    /// The condition is typically specified as a string expression and references the pipeline context data.
    /// The expression is evaluated at runtime and has to evaluate to <see langword="true"/> or <see langword="false"/>.
    /// If the condition evaluates to <see langword="true"/>, delivery of the pipeline data is allowed.
    /// If the condition evaluates to <see langword="false"/> or any other non-boolean value, delivery of the pipeline data is not allowed.
    /// </remarks>
    [YamlMember(Alias = "delivery_condition")]
    public string? DeliveryCondition { get; set; }
}
