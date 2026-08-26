using Geopilot.Pipeline;

namespace Geopilot.Api.Models;

/// <summary>
/// The protocol record of one evaluated step condition: which condition was checked, against which values,
/// and whether it matched. Recorded for every evaluated condition, not only for matching ones, so
/// "checked and did not apply" stays distinguishable from "never checked". This is the durable answer to
/// why a step was skipped, warned, failed, or restricted delivery.
/// </summary>
public class PipelineRunCondition
{
    /// <summary>
    /// The unique identifier for the condition record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The step record this evaluation belongs to.
    /// </summary>
    public int PipelineRunStepId { get; set; }

    /// <summary>
    /// Navigation to the step record.
    /// </summary>
    public PipelineRunStep Step { get; set; } = null!;

    /// <summary>
    /// The optional stable identifier of the condition from the pipeline definition. The
    /// machine-readable reason for a rejection, independent of message wording.
    /// </summary>
    public string? ConditionId { get; set; }

    /// <summary>
    /// The phase the condition was evaluated in.
    /// </summary>
    public ConditionPhase Phase { get; set; }

    /// <summary>
    /// The effect a match has on the step.
    /// </summary>
    public ConditionKind Kind { get; set; }

    /// <summary>
    /// The evaluated boolean expression.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Whether the expression evaluated to true.
    /// </summary>
    public bool Matched { get; set; }

    /// <summary>
    /// The values of the parameters the expression referenced, as a JSON object mapping parameter name
    /// to its rendered display string (collections reduced to their count, long values truncated).
    /// </summary>
    public string? EvaluatedValues { get; set; }
}
