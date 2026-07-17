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
    /// Resolves <paramref name="input"/> and coerces it to <paramref name="target"/>. Whether the
    /// target is a list or a single value decides the coercion: a list target collects its elements,
    /// while a single target expects one value (a one element list is unwrapped and a longer list is
    /// rejected). A missing input (<see langword="null"/>) is treated like a null value.
    /// </summary>
    /// <param name="target">The parameter the value is bound to.</param>
    /// <param name="input">The compiled input value, or <see langword="null"/> when none is configured.</param>
    /// <param name="resolveStepOutput">Resolves the value of an earlier step's output.</param>
    /// <returns>The value to pass to the process run method parameter.</returns>
    /// <exception cref="PipelineRunException">The input cannot be resolved or does not fit the target.</exception>
    internal static object? Bind(BindingTarget target, InputValue? input, StepOutputResolver resolveStepOutput)
    {
        if (TryGetListElementType(target.Type, out var elementType))
            return BindToList(target, elementType, input, resolveStepOutput);

        return BindToSingleValue(target, input, resolveStepOutput);
    }

    /// <summary>
    /// Determines whether the parameter is a list the binder fills: an array or the
    /// <see cref="IEnumerable{T}"/> interface itself. Concrete collection types such as
    /// <c>List&lt;T&gt;</c> are deliberately not treated as lists. Reports the element type on success.
    /// </summary>
    private static bool TryGetListElementType(Type parameterType, out Type elementType)
    {
        if (parameterType.IsArray)
        {
            elementType = parameterType.GetElementType()!;
            return true;
        }

        if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = parameterType.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    /// <summary>
    /// Binds to a single valued parameter. A resolved list contributes its only element; an empty
    /// list or a null value becomes null, which is accepted only when the parameter is nullable.
    /// </summary>
    private static object? BindToSingleValue(BindingTarget target, InputValue? input, StepOutputResolver resolveStepOutput)
    {
        var value = ResolveToSingleValue(target, input, resolveStepOutput);
        if (value is null)
        {
            if (target.IsNullable)
                return null;

            throw new PipelineRunException($"Input for parameter '{target.Name}' is null, but the parameter is not nullable.");
        }

        if (RawValueConverter.TryConvert(value, target.Type, out var converted))
            return converted;

        throw new PipelineRunException(
            $"Input for parameter '{target.Name}' of type <{value.GetType().Name}> cannot be converted to <{target.Type.Name}>.");
    }

    /// <summary>
    /// Reduces an input to the one value a single valued parameter expects. A list, whether it comes
    /// from a resolved reference or a written sequence, is unwrapped: no element becomes null, one
    /// element is that value, and more than one is rejected.
    /// </summary>
    private static object? ResolveToSingleValue(BindingTarget target, InputValue? input, StepOutputResolver resolveStepOutput)
    {
        if (input is InputValue.Sequence sequence)
            return UnwrapToSingleValue(target, sequence.Items.Select(item => Resolve(item, resolveStepOutput)).ToList());

        var resolved = Resolve(input, resolveStepOutput);
        return TryAsCollection(resolved, out var items) ? UnwrapToSingleValue(target, items) : resolved;
    }

    /// <summary>
    /// Unwraps a list to the single value a parameter expects: zero elements become null, exactly one
    /// is that element, and more than one cannot be a single value and is rejected.
    /// </summary>
    private static object? UnwrapToSingleValue(BindingTarget target, List<object?> items) => items.Count switch
    {
        0 => null,
        1 => items[0],
        _ => throw new PipelineRunException(
            $"Input for parameter '{target.Name}' resolved to {items.Count} values, but a single value is required."),
    };

    /// <summary>
    /// Binds to a list parameter (an array or <see cref="IEnumerable{T}"/>). A written sequence
    /// contributes each of its items; any other input contributes its single resolved value. A null
    /// value means there is no list at all and is accepted only when the parameter is nullable.
    /// </summary>
    private static Array? BindToList(BindingTarget target, Type elementType, InputValue? input, StepOutputResolver resolveStepOutput)
    {
        var elements = new List<object?>();

        if (input is InputValue.Sequence sequence)
        {
            foreach (var item in sequence.Items)
                AppendToList(target, elementType, elements, Resolve(item, resolveStepOutput));

            return CreateArray(elementType, elements);
        }

        var resolved = Resolve(input, resolveStepOutput);
        if (resolved is null)
        {
            if (target.IsNullable)
                return null;

            throw new PipelineRunException($"Input for parameter '{target.Name}' is null, but the parameter is not nullable.");
        }

        AppendToList(target, elementType, elements, resolved);
        return CreateArray(elementType, elements);
    }

    /// <summary>
    /// Adds a resolved value to the list under construction. A value that already is the element type
    /// becomes a single element; this is what lets a <c>list&lt;string&gt;</c> land as one row inside
    /// a <c>list&lt;list&lt;string&gt;&gt;</c> rather than being spread. A value that is instead a
    /// list of the element type is spread into its elements (one level deep). Any other value must
    /// convert to the element type on its own; a value that fits none of these is rejected.
    /// </summary>
    private static void AppendToList(BindingTarget target, Type elementType, List<object?> elements, object? value)
    {
        if (value is not null && elementType.IsInstanceOfType(value))
        {
            elements.Add(value);
        }
        else if (TryAsCollection(value, out var innerValues))
        {
            foreach (var innerValue in innerValues)
                elements.Add(ConvertToListElementType(target, elementType, innerValue));
        }
        else
        {
            elements.Add(ConvertToListElementType(target, elementType, value));
        }
    }

    /// <summary>
    /// Materializes the collected elements as an array of the element type. The array is assignable to
    /// an array parameter as well as to an <see cref="IEnumerable{T}"/> parameter.
    /// </summary>
    private static Array CreateArray(Type elementType, List<object?> elements)
    {
        var array = Array.CreateInstance(elementType, elements.Count);
        for (var i = 0; i < elements.Count; i++)
            array.SetValue(elements[i], i);

        return array;
    }

    /// <summary>
    /// Converts one value to the list element type, honoring element nullability. A null is allowed
    /// only when the element is nullable, and any other value must convert to the element type.
    /// </summary>
    private static object? ConvertToListElementType(BindingTarget target, Type elementType, object? value)
    {
        if (value is null)
        {
            if (target.IsElementNullable)
                return null;

            throw new PipelineRunException(
                $"An input element for parameter '{target.Name}' is null, but its element type is not nullable.");
        }

        if (RawValueConverter.TryConvert(value, elementType, out var converted))
            return converted;

        throw new PipelineRunException(
            $"An input element for parameter '{target.Name}' of type <{value.GetType().Name}> cannot be converted to <{elementType.Name}>.");
    }

    /// <summary>
    /// Resolves an input to its runtime value: a literal yields its raw text, a reference yields the
    /// earlier step output it points at, and a missing input yields null.
    /// </summary>
    private static object? Resolve(InputValue? input, StepOutputResolver resolveStepOutput) => input switch
    {
        null => null,
        InputValue.Literal literal => literal.Raw,
        InputValue.StepOutputReference reference => ResolveReference(reference, resolveStepOutput),
        _ => throw new PipelineRunException($"Unsupported input value kind <{input.GetType().Name}>."),
    };

    /// <summary>
    /// Resolves a step output reference to the value the earlier step published, or throws when no
    /// such output exists.
    /// </summary>
    private static object? ResolveReference(InputValue.StepOutputReference reference, StepOutputResolver resolveStepOutput)
    {
        if (resolveStepOutput(reference.StepId, reference.OutputName, out var value))
            return value;

        throw new PipelineRunException(
            $"Input references '{reference.StepId}.{reference.OutputName}', which is not an output of an earlier step.");
    }

    /// <summary>
    /// Views a value as a list of items, treating any enumerable other than a string as a collection.
    /// Returns <see langword="false"/> for a string, a scalar or null so the caller handles it as a
    /// single value.
    /// </summary>
    private static bool TryAsCollection(object? value, out List<object?> items)
    {
        if (value is System.Collections.IEnumerable enumerable and not string)
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
