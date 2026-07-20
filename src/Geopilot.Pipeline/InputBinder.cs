using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline;

/// <summary>
/// Resolves a compiled <see cref="InputValue"/> to a concrete value and coerces it to the type of
/// the process run method parameter it feeds. References (step outputs and files) are resolved
/// through the supplied <see cref="ReferenceResolver"/>.
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
    /// <param name="resolve">Resolves a reference (a step output or a file) to its runtime value.</param>
    /// <returns>The value to pass to the process run method parameter.</returns>
    /// <exception cref="PipelineRunException">The input cannot be resolved or does not fit the target.</exception>
    internal static object? Bind(BindingTarget target, InputValue? input, ReferenceResolver resolve)
    {
        if (TryGetListElementType(target.Type, out var elementType))
            return BindToList(target, elementType, input, resolve);

        return BindToSingleValue(target, input, resolve);
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
    private static object? BindToSingleValue(BindingTarget target, InputValue? input, ReferenceResolver resolve)
    {
        var value = ResolveToSingleValue(target, input, resolve);
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
    private static object? ResolveToSingleValue(BindingTarget target, InputValue? input, ReferenceResolver resolve)
    {
        if (input is InputValue.Sequence sequence)
            return UnwrapToSingleValue(target, sequence.Items.Select(item => Resolve(item, resolve)).ToList());

        var resolved = Resolve(input, resolve);
        if (resolved is not null && target.Type.IsInstanceOfType(resolved))
            return resolved;

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
    /// contributes each of its items, and a missing (unwired) input yields an empty list. Any other
    /// input contributes its single resolved value; a resolved null means there is no list and is
    /// accepted only when the parameter is nullable.
    /// </summary>
    private static Array? BindToList(BindingTarget target, Type elementType, InputValue? input, ReferenceResolver resolve)
    {
        var elements = new List<object?>();

        if (input is InputValue.Sequence sequence)
        {
            foreach (var item in sequence.Items)
                AppendToList(target, elementType, elements, Resolve(item, resolve));

            return CreateArray(elementType, elements);
        }

        if (input is null)
            return CreateArray(elementType, elements);

        var resolved = Resolve(input, resolve);
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
    /// Resolves an input to its runtime value: a literal yields its raw text, a reference (step output
    /// or file) is resolved through <paramref name="resolve"/>, and a missing input yields null.
    /// </summary>
    private static object? Resolve(InputValue? input, ReferenceResolver resolve) => input switch
    {
        null => null,
        InputValue.Literal literal => literal.Raw,
        InputValue.StepOutputReference reference => ResolveReference(reference, resolve),
        InputValue.FileReference reference => ResolveReference(reference, resolve),
        _ => throw new PipelineRunException($"Unsupported input value kind <{input.GetType().Name}>."),
    };

    /// <summary>
    /// Resolves a reference (a step output or a file) through <paramref name="resolve"/>, or throws a
    /// message describing the reference when it cannot be resolved in the current context.
    /// </summary>
    private static object? ResolveReference(InputValue reference, ReferenceResolver resolve)
    {
        if (resolve(reference, out var value))
            return value;

        throw new PipelineRunException(UnresolvedReferenceMessage(reference));
    }

    private static string UnresolvedReferenceMessage(InputValue reference) => reference switch
    {
        InputValue.StepOutputReference stepOutput =>
            $"Input references '{stepOutput.StepId}.{stepOutput.OutputName}', which is not an output of an earlier step.",
        InputValue.FileReference file =>
            $"Input references file '{file.RelativePath}', which could not be resolved.",
        _ => $"Input reference <{reference.GetType().Name}> could not be resolved.",
    };

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
/// Resolves an input reference (a <c>${step_output(...)}</c> or a <c>${file(...)}</c>) to its runtime
/// value. Returns <see langword="true"/> and the value when the reference resolves in the current
/// context, otherwise <see langword="false"/>, in which case the binder throws a message describing
/// the reference. May throw a <see cref="PipelineRunException"/> for a more specific failure.
/// </summary>
/// <param name="reference">The reference to resolve.</param>
/// <param name="value">The resolved value when the reference resolves.</param>
/// <returns><see langword="true"/> when the reference was resolved.</returns>
internal delegate bool ReferenceResolver(InputValue reference, out object? value);
