using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Reflection;

namespace Geopilot.Pipeline;

/// <summary>
/// Validates a pipeline step's input map against the run method of its process at load time: every
/// input key must target a bindable run method parameter, and a literal value must be convertible to
/// that parameter's type. Values sourced from an earlier step (<c>${step_output(...)}</c>) are not
/// type checked here because the source type is only known at run time.
/// </summary>
internal static class InputBindingValidator
{
    private static readonly StepOutputResolver NeverResolves = (string stepId, string outputName, out object? value) =>
    {
        value = null;
        return false;
    };

    /// <summary>
    /// Validates <paramref name="input"/> against the run method of <paramref name="processType"/>.
    /// </summary>
    /// <param name="processType">The resolved process implementation type.</param>
    /// <param name="input">The step's raw input map, keyed by target parameter name; may be null.</param>
    /// <returns>One message per problem found; empty when the input is valid.</returns>
    internal static IReadOnlyList<string> Validate(Type processType, InputConfig? input)
    {
        var errors = new List<string>();
        if (input is null || input.Count == 0)
            return errors;

        // Without exactly one run method the input cannot be bound at all; the run-time invocation
        // surfaces that separately, so there is nothing to validate against here.
        var runMethod = FindRunMethod(processType);
        if (runMethod is null)
            return errors;

        var bindableParameters = runMethod.GetParameters()
            .Where(p => p.Name is not null)
            .Where(p => p.GetCustomAttribute<UploadFilesAttribute>() is null)
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToDictionary(p => p.Name!, StringComparer.Ordinal);

        foreach (var (parameterName, rawValue) in input)
        {
            if (!bindableParameters.TryGetValue(parameterName, out var parameter))
            {
                errors.Add($"input '{parameterName}' does not match a parameter of the run method of process <{processType.Name}>.");
                continue;
            }

            InputValue compiled;
            try
            {
                compiled = InputCompiler.Compile(new Dictionary<string, object?> { [parameterName] = rawValue })[parameterName];
            }
            catch (InputCompilationException)
            {
                // A malformed reference is reported by the step-input reference validation; not here.
                continue;
            }

            // Only literals can be type checked at load time; a step output's type is known at run time.
            if (ReferencesEarlierStep(compiled))
                continue;

            try
            {
                InputBinder.Bind(BindingTarget.FromParameter(parameter), compiled, NeverResolves);
            }
            catch (PipelineRunException ex)
            {
                errors.Add($"input '{parameterName}': {ex.Message}");
            }
        }

        return errors;
    }

    private static MethodInfo? FindRunMethod(Type processType)
    {
        var runMethods = processType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessRunAttribute)))
            .Where(m => m.ReturnType == typeof(Task<Dictionary<string, object>>))
            .ToList();

        return runMethods.Count == 1 ? runMethods[0] : null;
    }

    private static bool ReferencesEarlierStep(InputValue value) => value switch
    {
        InputValue.StepOutputReference => true,
        InputValue.Sequence sequence => sequence.Items.Any(item => item is InputValue.StepOutputReference),
        _ => false,
    };
}
