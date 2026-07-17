namespace Geopilot.Pipeline.Config;

/// <summary>
/// Compiles the raw YAML input map of a pipeline step into a typed <see cref="InputValue"/> for
/// each process parameter.
/// </summary>
internal static class InputCompiler
{
    private const string StepOutputFunction = "step_output";
    private const string ReferenceMarker = "${";

    /// <summary>
    /// Compiles every entry of <paramref name="rawInput"/> into a typed <see cref="InputValue"/>. Each
    /// key is the name of the process run method parameter the value is bound to; each value is the raw
    /// YAML node the deserializer produced for it.
    /// </summary>
    /// <param name="rawInput">
    /// The raw input map of a step as deserialized from YAML, keyed by the name of the process run
    /// method parameter each value is bound to. Each value is the raw YAML node for that parameter: a
    /// scalar (a literal or a <c>${step_output(stepId.outputName)}</c> reference) or a sequence of those.
    /// </param>
    /// <returns>A compiled input value per parameter name.</returns>
    /// <exception cref="InputCompilationException">A value is not a supported input shape.</exception>
    internal static IReadOnlyDictionary<string, InputValue> Compile(IReadOnlyDictionary<string, object?> rawInput)
    {
        var compiled = new Dictionary<string, InputValue>(rawInput.Count);
        foreach (var (parameterName, rawValue) in rawInput)
        {
            compiled[parameterName] = CompileValue(parameterName, rawValue);
        }

        return compiled;
    }

    private static InputValue CompileValue(string parameterName, object? rawValue) => rawValue switch
    {
        null => new InputValue.Literal(null),
        string text => CompileScalar(text),

        // A YAML mapping (Dictionary<object, object>) would describe a complex object.
        // The definition of such objects in the pipeline YAML is not supported.
        System.Collections.IDictionary => throw new InputCompilationException(
            $"Input '{parameterName}': a complex object cannot be built in the definition. Fill it from a step output instead."),

        // A YAML sequence (List<object>) becomes a list value.
        System.Collections.IEnumerable sequence => CompileSequence(parameterName, sequence),

        _ => throw new InputCompilationException(
            $"Input '{parameterName}': only a literal, a ${{step_output(stepId.outputName)}} reference, or a list of those is supported here."),
    };

    /// <summary>
    /// Compiles a YAML sequence into a <see cref="InputValue.Sequence"/> by compiling each item on
    /// its own. A list may not contain another list, so an item that is itself a sequence is
    /// rejected rather than compiled.
    /// </summary>
    private static InputValue.Sequence CompileSequence(string parameterName, System.Collections.IEnumerable sequence)
    {
        var items = new List<InputValue>();
        foreach (var element in sequence)
        {
            var compiledElement = CompileValue(parameterName, element);
            if (compiledElement is InputValue.Sequence)
            {
                throw new InputCompilationException(
                    $"Input '{parameterName}': a list must not contain another list. Nested lists are not supported.");
            }

            items.Add(compiledElement);
        }

        return new InputValue.Sequence(items);
    }

    private static InputValue CompileScalar(string text)
    {
        // The marker "${" introduces a reference. A reference must be the whole value; a marker
        // embedded in surrounding text is rejected rather than kept as a literal.
        if (!text.Contains(ReferenceMarker, StringComparison.Ordinal))
            return new InputValue.Literal(text);

        return ParseReference(text);
    }

    private static InputValue.StepOutputReference ParseReference(string text)
    {
        if (!text.StartsWith(ReferenceMarker, StringComparison.Ordinal) || text[^1] != '}')
        {
            throw new InputCompilationException(
                $"Input '{text}' embeds a '${{...}}' reference marker in surrounding text. A reference must be the whole value, written as ${{step_output(stepId.outputName)}}.");
        }

        // Inside the braces a reference reads as a function call: name(argument).
        var body = text[2..^1];
        var open = body.IndexOf('(', StringComparison.Ordinal);
        if (open < 0 || body[^1] != ')' || body[..open] != StepOutputFunction)
        {
            throw new InputCompilationException(
                $"Reference '{text}' is not supported. Use ${{step_output(stepId.outputName)}}.");
        }

        var argument = body[(open + 1)..^1];
        var parts = argument.Split('.');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            throw new InputCompilationException(
                $"Reference '{text}' must be of the form ${{step_output(stepId.outputName)}}.");
        }

        return new InputValue.StepOutputReference(parts[0], parts[1]);
    }
}
