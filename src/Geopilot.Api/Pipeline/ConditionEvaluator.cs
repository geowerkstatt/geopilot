using Bogus.DataSets;
using NCalc;
using System.Globalization;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Provides functionality to evaluate conditional expressions within a pipeline context.
/// </summary>
/// <remarks>Use this class to determine whether specific conditions are met during pipeline execution by
/// evaluating expressions against the provided context. This type is typically used to support dynamic branching or
/// decision-making in pipeline workflows.</remarks>
public class ConditionEvaluator : IConditionEvaluator
{
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the ConditionEvaluator class.
    /// <param name="logger">The logger to use for logging.</param>
    /// </summary>
    public ConditionEvaluator(ILogger logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> EvaluateConditionAsync(string expression, Dictionary<string, object?> expressionParameters)
    {
        var matematicalExpression = new AsyncExpression(expression, ExpressionOptions.AllowNullParameter);

        matematicalExpression.GetParameterNames()
            .ForEach(paramName =>
            {
                if (expressionParameters.TryGetValue(paramName, out var parameterValue))
                    matematicalExpression.Parameters[paramName] = parameterValue;
            });

        var expressionResult = await matematicalExpression.EvaluateAsync();
        if (expressionResult is bool)
        {
            return Convert.ToBoolean(expressionResult, CultureInfo.InvariantCulture);
        }
        else
        {
            logger.LogWarning($"Expression '{expression}' did not evaluate to a boolean value. Result: {expressionResult}");
            return false;
        }
    }
}
