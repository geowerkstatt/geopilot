namespace Geopilot.Api.Pipeline;

/// <summary>
/// Defines a contract for evaluating a condition expression within a pipeline context.
/// </summary>
/// <remarks>Implementations of this interface are responsible for interpreting the provided condition string and
/// determining its truth value based on the supplied pipeline context. This interface is typically used to enable
/// conditional logic in pipeline execution scenarios, such as controlling step execution or allow/prevent deliveries.</remarks>
public interface IConditionEvaluator
{
    /// <summary>
    /// Evaluates a logical condition expression within the specified pipeline context.
    /// </summary>
    /// <remarks>The evaluation depends on the variables and state available in the provided pipeline context.</remarks>
    /// <param name="expression">The condition expression to evaluate. Must be a valid logical expression supported by the pipeline.
    /// Parameters of this expression reference to the pipeline context data.</param>
    /// <param name="expressionParameters">The expression parameters containing a key the parameter name in the format 'stepId.resultId' and as value the parameters value.</param>
    /// <returns>true if the condition evaluates to true in the given context; otherwise, false. It the referenced expression parameters are not present, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the expression is syntacticall invalid.</exception>"
    Task<bool> EvaluateConditionAsync(string expression, Dictionary<string, object?> expressionParameters);
}
