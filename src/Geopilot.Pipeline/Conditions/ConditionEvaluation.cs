namespace Geopilot.Pipeline;

/// <summary>
/// The phase in which a step condition is evaluated.
/// </summary>
public enum ConditionPhase
{
    /// <summary>Evaluated before the step's process runs, against earlier steps' results.</summary>
    Pre,

    /// <summary>Evaluated after the step's process ran, additionally against the step's own result.</summary>
    Post,
}

/// <summary>
/// The effect a matching condition has on the step.
/// </summary>
public enum ConditionKind
{
    /// <summary>A match fails the step.</summary>
    Fail,

    /// <summary>A match skips the step. Pre phase only.</summary>
    Skip,

    /// <summary>A match completes the step with a warning. Post phase only.</summary>
    Warn,

    /// <summary>A match completes the step but blocks delivering the job. Post phase only.</summary>
    RestrictDelivery,
}

/// <summary>
/// The evaluation result of a single step condition: which condition was checked, against which values,
/// and whether it matched. Recorded for every evaluated condition, not only for matching ones, so a
/// consumer can tell "checked and did not apply" apart from "never checked".
/// </summary>
/// <param name="ConditionId">The optional stable identifier of the condition from the pipeline definition.</param>
/// <param name="Phase">The phase the condition was evaluated in.</param>
/// <param name="Kind">The effect a match has on the step.</param>
/// <param name="Expression">The evaluated boolean expression.</param>
/// <param name="Matched">Whether the expression evaluated to true.</param>
/// <param name="Parameters">The values of the parameters the expression references, rendered to display strings (collections reduced to their count, long values truncated).</param>
public sealed record ConditionEvaluation(
    string? ConditionId,
    ConditionPhase Phase,
    ConditionKind Kind,
    string Expression,
    bool Matched,
    IReadOnlyDictionary<string, string?> Parameters);
