using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline;

/// <summary>
/// Resolves a compiled <see cref="InputValue"/> to a concrete value and coerces it to the type of
/// the process run method parameter it feeds. Step output references are resolved against the
/// outputs of earlier steps through the supplied <see cref="StepOutputResolver"/>.
/// </summary>
internal static class InputBinder
{
    /// <summary>
    /// Resolves <paramref name="input"/> and coerces it to <paramref name="target"/>. A missing
    /// input (<see langword="null"/>) is treated the same as a null value: allowed only when the
    /// target is nullable. When the resolved value is a list and the target is a single value, a
    /// one element list is unwrapped and a longer list is rejected.
    /// </summary>
    /// <param name="target">The parameter the value is bound to.</param>
    /// <param name="input">The compiled input value, or <see langword="null"/> when none is configured.</param>
    /// <param name="resolveStepOutput">Resolves the value of an earlier step's output.</param>
    /// <returns>The value to pass to the process run method parameter.</returns>
    /// <exception cref="PipelineRunException">The input cannot be resolved or does not fit the target.</exception>
    internal static object? Bind(BindingTarget target, InputValue? input, StepOutputResolver resolveStepOutput)
    {
        var resolved = Resolve(target, input, resolveStepOutput);
        return target.Type.IsArray
            ? CoerceToArray(target, resolved)
            : CoerceToSingle(target, resolved);
    }

    private static object? Resolve(BindingTarget target, InputValue? input, StepOutputResolver resolveStepOutput) => input switch
    {
        null => null,
        InputValue.Literal literal => literal.Raw,
        InputValue.StepOutputReference reference => ResolveReference(reference, resolveStepOutput),
        _ => throw new PipelineRunException($"Input for parameter '{target.Name}' has an unsupported value kind."),
    };

    private static object? ResolveReference(InputValue.StepOutputReference reference, StepOutputResolver resolveStepOutput)
    {
        if (resolveStepOutput(reference.StepId, reference.OutputName, out var value))
            return value;

        throw new PipelineRunException(
            $"Input references '{reference.StepId}.{reference.OutputName}', which is not an output of an earlier step.");
    }

    private static object? CoerceToSingle(BindingTarget target, object? resolved)
    {
        if (TryAsCollection(resolved, out var items))
        {
            resolved = items.Count switch
            {
                0 => null,
                1 => items[0],
                _ => throw new PipelineRunException(
                    $"Input for parameter '{target.Name}' resolved to {items.Count} values, but a single value is required."),
            };
        }

        if (resolved is null)
        {
            if (target.IsNullable)
                return null;

            throw new PipelineRunException($"Input for parameter '{target.Name}' is null, but the parameter is not nullable.");
        }

        if (RawValueConverter.TryConvert(resolved, target.Type, out var converted))
            return converted;

        throw new PipelineRunException(
            $"Input for parameter '{target.Name}' of type <{resolved.GetType().Name}> cannot be converted to <{target.Type.Name}>.");
    }

    private static Array CoerceToArray(BindingTarget target, object? resolved)
    {
        var elementType = target.Type.GetElementType()
            ?? throw new PipelineRunException($"Array parameter '{target.Name}' has no element type.");

        var items = TryAsCollection(resolved, out var collection)
            ? collection
            : new List<object?> { resolved };

        var array = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            array.SetValue(CoerceElement(target, elementType, items[i]), i);
        }

        return array;
    }

    private static object? CoerceElement(BindingTarget target, Type elementType, object? item)
    {
        if (item is null)
        {
            if (target.IsElementNullable)
                return null;

            throw new PipelineRunException(
                $"An input element for array parameter '{target.Name}' is null, but its element type is not nullable.");
        }

        if (RawValueConverter.TryConvert(item, elementType, out var converted))
            return converted;

        throw new PipelineRunException(
            $"An input element for array parameter '{target.Name}' of type <{item.GetType().Name}> cannot be converted to <{elementType.Name}>.");
    }

    private static bool TryAsCollection(object? value, out List<object?> items)
    {
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            items = enumerable.Cast<object?>().ToList();
            return true;
        }

        items = [];
        return false;
    }
}

/// <summary>
/// Resolves the value an earlier step published under the given output name. Returns
/// <see langword="true"/> and the value when the output exists, otherwise <see langword="false"/>.
/// </summary>
/// <param name="stepId">The id of the earlier step.</param>
/// <param name="outputName">The name of the output on that step.</param>
/// <param name="value">The resolved value when the output exists.</param>
/// <returns><see langword="true"/> when the output exists.</returns>
internal delegate bool StepOutputResolver(string stepId, string outputName, out object? value);
