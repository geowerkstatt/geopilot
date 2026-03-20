namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Specifies the types of constraints that can be applied within a validation or data integrity context.
/// </summary>
/// <remarks>Use the members of this enumeration to indicate the nature of a constraint, such as whether it is
/// mandatory, relates to plausibility, existence, uniqueness, or is defined by a set. The meaning and enforcement of
/// each constraint type may vary depending on the domain or validation framework in use.</remarks>
public enum ConstraintType
{
    /// <summary>
    /// Gets or sets a value indicating whether the associated field or property is required for validation purposes.
    /// </summary>
    Mandatory,

    /// <summary>
    /// Represents the plausibility status or value used to indicate the likelihood or credibility of a condition,
    /// event, or result.
    /// </summary>
    Plausibility,

    /// <summary>
    /// Represents the existence state or presence of an entity or object.
    /// </summary>
    Existence,

    /// <summary>
    /// Represents a value or concept that is unique within a given context.
    /// </summary>
    Uniqueness,

    /// <summary>
    /// Gets or sets the value associated with this property.
    /// </summary>
    Set,
}
