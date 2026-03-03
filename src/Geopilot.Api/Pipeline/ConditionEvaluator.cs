using NCalc;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Provides functionality to evaluate conditional expressions within a pipeline context.
/// </summary>
/// <remarks>Use this class to determine whether specific conditions are met during pipeline execution by
/// evaluating expressions against the provided context. This type is typically used to support dynamic branching or
/// decision-making in pipeline workflows.</remarks>
public class ConditionEvaluator : IConditionEvaluator
{
    private static char parameterSeparator = '.';
    private static string parameterPattern = "^(\\w+)[" + parameterSeparator + "](\\w+)$";

    private readonly ILogger<ConditionEvaluator> logger;

    /// <summary>
    /// Initializes a new instance of the ConditionEvaluator class.
    /// </summary>
    public ConditionEvaluator()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<ConditionEvaluator>();
    }

    /// <inheritdoc />
    public async Task<bool> EvaluateConditionAsync(string expression, PipelineContext context)
    {
        var matematicalExpression = new AsyncExpression(expression, ExpressionOptions.AllowNullParameter);
        matematicalExpression.EvaluateParameterAsync += async (name, args) =>
        {
            var match = Regex.Match(name, parameterPattern);
            if (match.Success)
            {
                var parameterSplit = name.Split(parameterSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
                var stepId = parameterSplit[0];
                if (context.StepResults.TryGetValue(stepId, out var stepResult))
                {
                    var outputId = parameterSplit[1];
                    if (stepResult.Outputs.TryGetValue(outputId, out var output))
                    {
                        args.Result = output.Data;
                    }
                    else
                    {
                        logger.LogWarning($"Output '{outputId}' was not found in step '{stepId}'.");
                    }
                }
                else
                {
                    logger.LogWarning($"step '{stepId}' was not found in the pipeline context.");
                }
            }
        };

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
