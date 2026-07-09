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
    /// Compiles every entry of <paramref name="rawInput"/>. Each key is the target parameter name
    /// and each value is the raw YAML node produced by the deserializer.
    /// </summary>
    /// <param name="rawInput">The raw input map of a step, as deserialized from YAML.</param>
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

        // A YAML sequence (List<object>) or mapping (Dictionary<object, object>) lands here.
        // Neither is supported yet: only literals and ${step_output(...)} references are.
        _ => throw new InputCompilationException(
            $"Input '{parameterName}': only a literal or a ${{step_output(stepId.outputName)}} reference is supported here."),
    };

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
