using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Reflection;

namespace Geopilot.Pipeline;

/// <summary>
/// Validates a pipeline step's input map against the run method of its process at load time: every
/// input key must target a bindable run method parameter, and a literal value must be convertible to
/// that parameter's type. Values sourced from an earlier step (<c>${step_output(...)}</c>) are not
/// type checked here because the source type is only known at run time. A <c>${file(path)}</c>
/// reference is type checked against the parameter and, when the resources root is known, verified to
/// exist under it.
/// </summary>
internal static class InputBindingValidator
{
    private static readonly ReferenceResolver NeverResolves = (InputValue reference, out object? value) =>
    {
        value = null;
        return false;
    };

    /// <summary>
    /// Validates <paramref name="input"/> against the run method of <paramref name="processType"/>.
    /// </summary>
    /// <param name="processType">The resolved process implementation type.</param>
    /// <param name="input">The step's raw input map, keyed by target parameter name; may be null.</param>
    /// <param name="resourcesRoot">The resources root that <c>${file(path)}</c> references resolve against; when null, file existence is not checked.</param>
    /// <returns>One message per problem found; empty when the input is valid.</returns>
    internal static IReadOnlyList<string> Validate(Type processType, InputConfig? input, string? resourcesRoot = null)
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

            if (ContainsFileReference(compiled))
            {
                ValidateFileReference(parameterName, parameter, compiled, resourcesRoot, errors);
                continue;
            }

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

    private static bool ContainsFileReference(InputValue value) => value switch
    {
        InputValue.FileReference => true,
        InputValue.Sequence sequence => sequence.Items.Any(item => item is InputValue.FileReference),
        _ => false,
    };

    private static IEnumerable<string> FilePathsOf(InputValue value) => value switch
    {
        InputValue.FileReference file => new[] { file.RelativePath },
        InputValue.Sequence sequence => sequence.Items.OfType<InputValue.FileReference>().Select(item => item.RelativePath),
        _ => Enumerable.Empty<string>(),
    };

    // A stand-in IPipelineFile used only to type check a file reference against the target parameter;
    // the binder inspects the value's type and never reads the file.
    private static readonly IPipelineFile SentinelFile = new PipelineFile("sentinel", "sentinel");

    private static readonly ReferenceResolver ResolvesToSentinelFile = (InputValue reference, out object? value) =>
    {
        if (reference is InputValue.FileReference)
        {
            value = SentinelFile;
            return true;
        }

        value = null;
        return false;
    };

    /// <summary>
    /// Validates a file reference: its type against the target parameter (through the real binder with
    /// a sentinel file) and, when the resources root is known, the existence of each referenced file.
    /// </summary>
    private static void ValidateFileReference(string parameterName, ParameterInfo parameter, InputValue compiled, string? resourcesRoot, List<string> errors)
    {
        try
        {
            InputBinder.Bind(BindingTarget.FromParameter(parameter), compiled, ResolvesToSentinelFile);
        }
        catch (PipelineRunException ex)
        {
            errors.Add($"input '{parameterName}': {ex.Message}");
        }

        if (resourcesRoot is null)
            return;

        foreach (var relativePath in FilePathsOf(compiled))
        {
            string fullPath;
            try
            {
                fullPath = ResourceFileResolver.ResolveFullPath(resourcesRoot, relativePath);
            }
            catch (PipelineRunException ex)
            {
                errors.Add($"input '{parameterName}': {ex.Message}");
                continue;
            }

            if (!File.Exists(fullPath))
                errors.Add($"input '{parameterName}' references file '{relativePath}', which does not exist under the resources directory.");
        }
    }
}
