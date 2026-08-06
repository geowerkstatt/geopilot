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
    /// <param name="stepResultTypes">Maps each earlier step's id to its process result type. Used to validate <c>${step_output(stepId.output)}</c> references (that the output exists and its type is bindable to the target parameter); when null, step output references are not type checked.</param>
    /// <returns>One message per problem found; empty when the input is valid.</returns>
    internal static IReadOnlyList<string> Validate(Type processType, InputConfig? input, string? resourcesRoot, IReadOnlyDictionary<string, Type>? stepResultTypes)
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

            if (ReferencesEarlierStep(compiled))
            {
                ValidateStepOutputReferences(parameter, compiled, stepResultTypes, errors);
                continue;
            }

            if (ContainsFileReference(compiled) || ContainsUploadReference(compiled))
            {
                ValidateResolvableReference(parameterName, parameter, compiled, resourcesRoot, errors);
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

    /// <summary>
    /// Validates the <c>${step_output(stepId.output)}</c> references in a step's input: the referenced
    /// output must be a readable property of the referenced step's result type, and its type must be
    /// bindable to the target run method parameter. No-op when <paramref name="stepResultTypes"/> is null.
    /// </summary>
    private static void ValidateStepOutputReferences(ParameterInfo parameter, InputValue compiled, IReadOnlyDictionary<string, Type>? stepResultTypes, List<string> errors)
    {
        if (stepResultTypes is null)
            return;

        foreach (var reference in StepOutputReferencesOf(compiled))
        {
            if (!stepResultTypes.TryGetValue(reference.StepId, out var resultType))
                continue;

            var property = resultType.GetProperty(reference.OutputName);
            if (property is null || !property.CanRead)
            {
                errors.Add($"input '{parameter.Name}' references '{reference.StepId}.{reference.OutputName}', which is not a readable property of the result type <{resultType.Name}>.");
                continue;
            }

            if (!IsBindable(property.PropertyType, parameter.ParameterType))
                errors.Add($"input '{parameter.Name}' references '{reference.StepId}.{reference.OutputName}' of type <{property.PropertyType.Name}>, which is not compatible with the parameter type <{parameter.ParameterType.Name}>.");
        }
    }

    /// <summary>
    /// The step output references in an input value: the value itself when it is a reference, or the
    /// reference items of a sequence; empty otherwise.
    /// </summary>
    private static IEnumerable<InputValue.StepOutputReference> StepOutputReferencesOf(InputValue value)
    {
        if (value is InputValue.StepOutputReference reference)
            return new[] { reference };
        if (value is InputValue.Sequence sequence)
            return sequence.Items.OfType<InputValue.StepOutputReference>();
        return Enumerable.Empty<InputValue.StepOutputReference>();
    }

    /// <summary>
    /// Whether a value of <paramref name="sourceType"/> can bind to a parameter of
    /// <paramref name="parameterType"/> for at least some run-time value, mirroring the binder without
    /// duplicating its rules: it reuses the binder's list detection
    /// (<see cref="InputBinder.TryGetListElementType"/> for the target,
    /// <see cref="InputBinder.SpreadableElementType"/> for a collection source) and its string conversions
    /// (<see cref="RawValueConverter.IsStringConvertibleTarget"/>). A single-value target binds when the
    /// source is assignable in either direction, when a collection source unwraps to a bindable element,
    /// when a string converts to it, or when it is a concrete type the binder's JSON round-trip could
    /// produce; only an interface or abstract target the source is not assignable to is rejected. Kept no
    /// stricter than the binder so a valid pipeline is not rejected at load time.
    /// </summary>
    private static bool IsBindable(Type sourceType, Type parameterType)
    {
        if (InputBinder.TryGetListElementType(parameterType, out var listElementType))
        {
            var collectionElement = InputBinder.SpreadableElementType(sourceType);
            return collectionElement is not null
                ? IsBindable(collectionElement, listElementType)
                : IsBindable(sourceType, listElementType);
        }

        if (parameterType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(parameterType))
            return true;

        var sourceElement = InputBinder.SpreadableElementType(sourceType);
        if (sourceElement is not null)
            return IsBindable(sourceElement, parameterType);

        if (sourceType == typeof(string) && RawValueConverter.IsStringConvertibleTarget(parameterType))
            return true;

        var target = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        return !target.IsInterface && !target.IsAbstract;
    }

    private static MethodInfo? FindRunMethod(Type processType)
    {
        var runMethods = processType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessRunAttribute)))
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

    private static bool ContainsUploadReference(InputValue value) => value switch
    {
        InputValue.UploadReference => true,
        InputValue.Sequence sequence => sequence.Items.Any(item => item is InputValue.UploadReference),
        _ => false,
    };

    private static IEnumerable<string> FilePathsOf(InputValue value) => value switch
    {
        InputValue.FileReference file => new[] { file.RelativePath },
        InputValue.Sequence sequence => sequence.Items.OfType<InputValue.FileReference>().Select(item => item.RelativePath),
        _ => Enumerable.Empty<string>(),
    };

    // Stand-in values used only to type check a file or upload reference against the target parameter;
    // the binder inspects the value's type and never reads them.
    private static readonly IPipelineFile SentinelFile = new PipelineFile("sentinel", "sentinel");
    private static readonly IPipelineFile[] SentinelFiles = new IPipelineFile[] { SentinelFile };

    private static readonly ReferenceResolver ResolvesToSentinel = (InputValue reference, out object? value) =>
    {
        value = reference switch
        {
            InputValue.FileReference => SentinelFile,
            InputValue.UploadReference => SentinelFiles,
            _ => null,
        };
        return value is not null;
    };

    /// <summary>
    /// Validates a file or upload reference: its type against the target parameter (through the real
    /// binder with a sentinel value) and, for file references when the resources root is known, the
    /// existence of each referenced file.
    /// </summary>
    private static void ValidateResolvableReference(string parameterName, ParameterInfo parameter, InputValue compiled, string? resourcesRoot, List<string> errors)
    {
        try
        {
            InputBinder.Bind(BindingTarget.FromParameter(parameter), compiled, ResolvesToSentinel);
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
