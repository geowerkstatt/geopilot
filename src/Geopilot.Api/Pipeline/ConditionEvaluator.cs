using NCalc;
using NCalc.Handlers;
using System.Collections;
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
        var runner = CreateRunner(expression, logger);
        runner.RegisterParameters(expressionParameters);
        return await runner.EvaluateConditionAsync();
    }

    /// <summary>
    /// Creates a new instance of the ConditionEvaluatorRunner using the specified expression and logger.
    /// </summary>
    /// <param name="expression">The condition expression to evaluate. This string defines the logic that the runner will process.</param>
    /// <param name="logger">The logger to use for recording evaluation events and errors. Cannot be null.</param>
    /// <returns>A ConditionEvaluatorRunner initialized with the provided expression and logger.</returns>
    public static ConditionEvaluatorRunner CreateRunner(string expression, ILogger logger)
    {
        return new ConditionEvaluatorRunner(expression, logger);
    }

    /// <summary>
    /// Provides functionality to evaluate boolean expressions asynchronously using supplied parameters and custom
    /// functions.
    /// </summary>
    /// <remarks>This class allows dynamic evaluation of expressions where parameters can be registered at
    /// runtime. It logs a warning if the evaluated expression does not return a boolean value. Instances are intended
    /// for scenarios where expression logic needs to be evaluated based on runtime data, such as feature toggling or
    /// conditional workflows.</remarks>
    public class ConditionEvaluatorRunner
    {
        private readonly ILogger logger;
        private readonly AsyncExpression expression;

        /// <summary>
        /// Initializes a new instance of the ConditionEvaluatorRunner class with the specified expression and logger.
        /// </summary>
        /// <param name="expression">The expression to evaluate. This string defines the condition logic to be processed by the evaluator.</param>
        /// <param name="logger">The logger used to record diagnostic or error information during evaluation.</param>
        public ConditionEvaluatorRunner(string expression, ILogger logger)
        {
            this.expression = new AsyncExpression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache);
            this.RegisterCustomFunctions();
            this.logger = logger;
        }

        /// <summary>
        /// Registers parameter values for the current expression using the specified dictionary.
        /// </summary>
        /// <remarks>Parameters in the expression that do not have a corresponding entry in the dictionary
        /// are not modified.</remarks>
        /// <param name="expressionParameters">A dictionary containing parameter names and their corresponding values to assign to the expression. Only
        /// parameters present in both the expression and the dictionary are registered. Parameter values may be null.</param>
        internal void RegisterParameters(Dictionary<string, object?> expressionParameters)
        {
            GetParameterNames()
                .ForEach(paramName =>
                {
                    if (expressionParameters.TryGetValue(paramName, out var parameterValue))
                        expression.Parameters[paramName] = parameterValue;
                });
        }

        /// <summary>
        /// Retrieves the names of all parameters used in the expression.
        /// </summary>
        /// <returns>A list of strings containing the names of the parameters. The list is empty if the expression does not
        /// contain any parameters.</returns>
        public List<string> GetParameterNames()
        {
            return expression.GetParameterNames();
        }

        /// <summary>
        /// Asynchronously evaluates the underlying expression and determines whether it resolves to a Boolean value.
        /// </summary>
        /// <remarks>If the expression does not evaluate to a Boolean value, the method logs a warning and
        /// returns <see langword="false"/>.</remarks>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
        /// expression evaluates to a Boolean value of <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> EvaluateConditionAsync()
        {
            var expressionResult = await expression.EvaluateAsync();
            if (expressionResult is bool)
            {
                return Convert.ToBoolean(expressionResult, CultureInfo.InvariantCulture);
            }
            else
            {
                logger.LogWarning($"Expression '{expression.ExpressionString}' did not evaluate to a boolean value. Result: {expressionResult}");
                return false;
            }
        }

        /// <summary>
        /// Registers custom functions with the expression evaluator to extend its capabilities.
        /// </summary>
        /// <remarks>Call this method to add user-defined functions that can be invoked during expression
        /// evaluation. This enables support for additional operations beyond the built-in set.</remarks>
        private void RegisterCustomFunctions()
        {
            this.expression.EvaluateFunctionAsync += lengthFunction;
        }

        private static readonly AsyncEvaluateFunctionHandler lengthFunction = async (name, args) =>
        {
            if (name == "Length")
            {
                if (args.Parameters.Length != 1)
                    throw new ArgumentException("Length() requires exactly 1 argument.");
                var value = await args.Parameters[0].EvaluateAsync();
                args.Result = value switch
                {
                    Array array => array.Length,
                    ICollection collection => collection.Count,
                    null => throw new ArgumentException("Length() does not support null arguments."),
                    _ => throw new ArgumentException($"Length() requires an array or collection argument but got {value.GetType().Name}."),
                };
            }
        };
    }
}
