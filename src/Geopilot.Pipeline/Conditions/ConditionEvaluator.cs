using Microsoft.Extensions.Logging;
using NCalc;
using NCalc.Handlers;
using System.Collections;
using System.Globalization;

namespace Geopilot.Pipeline;

/// <summary>
/// Provides functionality to evaluate conditional expressions within a pipeline context.
/// </summary>
/// <remarks>Use this class to determine whether specific conditions are met during pipeline execution by
/// evaluating expressions against the provided context. This type is typically used to support dynamic branching or
/// decision-making in pipeline workflows.</remarks>
internal class ConditionEvaluator : IConditionEvaluator
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
    public async Task<ConditionEvaluatorResult> EvaluateConditionAsync(
        string expression,
        Dictionary<string, object?> expressionParameters,
        IReadOnlySet<string>? stepsWithoutResult = null)
    {
        var runner = CreateRunner(expression, logger);

        // Parsed once, and registered and reported from the same set, so a consumer sees exactly the
        // values the expression saw.
        var referencedParameters = ResolveParameters(runner.GetParameterNames(), expressionParameters, stepsWithoutResult);
        runner.RegisterParameters(referencedParameters);

        var matched = await runner.EvaluateConditionAsync();
        return new ConditionEvaluatorResult(matched, referencedParameters);
    }

    /// <summary>
    /// Picks the values for the parameter names the expression references. A name without a value is
    /// kept as null when it belongs to a step that ran no process, which is what a skipped step is.
    /// A name belonging to a step that did produce a result is left out, so a reference that names no
    /// real output still fails loudly instead of quietly reading as null.
    /// </summary>
    private static Dictionary<string, object?> ResolveParameters(
        List<string> parameterNames,
        Dictionary<string, object?> expressionParameters,
        IReadOnlySet<string>? stepsWithoutResult)
    {
        var referenced = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in parameterNames.Distinct())
        {
            if (expressionParameters.TryGetValue(name, out var value))
            {
                referenced[name] = value;
            }
            else if (stepsWithoutResult?.Contains(StepIdOf(name)) == true)
            {
                referenced[name] = null;
            }
        }

        return referenced;
    }

    /// <summary>
    /// The step id part of a parameter name in the format <c>stepId.resultId</c>.
    /// </summary>
    private static string StepIdOf(string parameterName)
    {
        var separator = parameterName.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? parameterName : parameterName[..separator];
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
    internal class ConditionEvaluatorRunner
    {
        private readonly ILogger logger;
        private readonly Expression expression;

        /// <summary>
        /// Initializes a new instance of the ConditionEvaluatorRunner class with the specified expression and logger.
        /// </summary>
        /// <param name="expression">The expression to evaluate. This string defines the condition logic to be processed by the evaluator.</param>
        /// <param name="logger">The logger used to record diagnostic or error information during evaluation.</param>
        public ConditionEvaluatorRunner(string expression, ILogger logger)
        {
            this.expression = new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache);
            this.RegisterCustomFunctions();
            this.logger = logger;
        }

        /// <summary>
        /// Registers parameter values for the current expression.
        /// </summary>
        /// <remarks>Parameters the expression references but that are absent from <paramref name="parameters"/>
        /// stay unregistered, which makes the evaluation throw.</remarks>
        /// <param name="parameters">The values to assign, already reduced to the names the expression references.
        /// Values may be null.</param>
        internal void RegisterParameters(Dictionary<string, object?> parameters)
        {
            foreach (var parameter in parameters)
                expression.Parameters[parameter.Key] = parameter.Value;
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
            this.expression.AsyncFunctions["Length"] = lengthFunction;
        }

        private static readonly AsyncExpressionFunction lengthFunction = async parameters =>
        {
            if (parameters.Count != 1)
                throw new ArgumentException("Length() requires exactly 1 argument.");

            var value = await parameters.EvaluateAsync(0);
            return value switch
            {
                Array array => array.Length,
                ICollection collection => collection.Count,
                null => 0,
                _ => throw new ArgumentException($"Length() requires an array or collection argument but got {value.GetType().Name}."),
            };
        };
    }
}
