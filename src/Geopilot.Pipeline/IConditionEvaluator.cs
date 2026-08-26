namespace Geopilot.Pipeline;

/// <summary>
/// Defines a contract for evaluating a condition expression within a pipeline context.
/// </summary>
/// <remarks>Implementations of this interface are responsible for interpreting the provided condition string and
/// determining its truth value based on the supplied pipeline context. This interface is typically used to enable
/// conditional logic in pipeline execution scenarios, such as controlling step execution or allow/prevent deliveries.</remarks>
internal interface IConditionEvaluator
{
    /// <summary>
    /// Evaluates a logical condition expression within the specified pipeline context.
    /// </summary>
    /// <remarks>The evaluation depends on the variables and state available in the provided pipeline context.</remarks>
    /// <param name="expression">The condition expression to evaluate. Must be a valid logical expression supported by the pipeline.
    /// Parameters of this expression reference to the pipeline context data.</param>
    /// <param name="expressionParameters">The expression parameters containing a key the parameter name in the format 'stepId.resultId' and as value the parameters value.</param>
    /// <returns>Whether the condition matched, together with the raw values of the parameters the expression
    /// references. A non-boolean expression result counts as not matched.</returns>
    /// <remarks>A syntactically invalid expression, or one referencing a parameter that is not present (e.g.
    /// an output of a step that was skipped), makes the evaluation throw. The step then ends in
    /// <see cref="StepState.Error"/>; it does not count as "not matched".</remarks>
    Task<ConditionEvaluatorResult> EvaluateConditionAsync(string expression, Dictionary<string, object?> expressionParameters);
}

/// <summary>
/// The outcome of evaluating one condition expression: whether it matched, and the raw values of the
/// parameters the expression references (limited to those present in the supplied parameter set).
/// </summary>
/// <param name="Matched">Whether the expression evaluated to true.</param>
/// <param name="ReferencedParameters">The raw values of the parameters the expression references.</param>
internal sealed record ConditionEvaluatorResult(bool Matched, IReadOnlyDictionary<string, object?> ReferencedParameters);
