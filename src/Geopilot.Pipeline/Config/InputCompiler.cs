using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Compiles the raw YAML input map of a pipeline step into a typed <see cref="InputValue"/> for
/// each process parameter.
/// </summary>
internal static partial class InputCompiler
{
    // Matches a value that is entirely a ${...} reference and captures the content between the braces.
    // It requires both the opening "${" and the closing "}"; a marker embedded in surrounding text or
    // a missing closing brace does not match.
    [GeneratedRegex(@"^\$\{(.+)\}$")]
    private static partial Regex ReferencePattern();

    // Matches a step_output(...) call and captures the argument between the parentheses.
    [GeneratedRegex(@"^\s*step_output\s*\(\s*(.*?)\s*\)\s*$")]
    private static partial Regex StepOutputCallPattern();

    // Matches a stepId.outputName argument and captures both identifiers (no whitespace, dots or
    // parentheses in either).
    [GeneratedRegex(@"^\s*([^.()\s]+)\s*\.\s*([^.()\s]+)\s*$")]
    private static partial Regex StepOutputArgumentPattern();

    // Matches a file(...) call and captures the path between the parentheses.
    [GeneratedRegex(@"^\s*file\s*\(\s*(.*?)\s*\)\s*$")]
    private static partial Regex FileCallPattern();

    /// <summary>
    /// Compiles every entry of <paramref name="rawInput"/> into a typed <see cref="InputValue"/>. Each
    /// key is the name of the process run method parameter the value is bound to; each value is the raw
    /// YAML node the deserializer produced for it.
    /// </summary>
    /// <param name="rawInput">
    /// The raw input map of a step as deserialized from YAML, keyed by the name of the process run
    /// method parameter each value is bound to. Each value is the raw YAML node for that parameter: a
    /// scalar (a literal, a <c>${step_output(stepId.outputName)}</c> reference or a <c>${file(path)}</c>
    /// reference) or a sequence of those.
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
        // A value without the "${" marker is a plain literal.
        if (!text.Contains("${", StringComparison.Ordinal))
            return new InputValue.Literal(text);

        // A reference must be the whole value, ${...} with both braces (surrounding whitespace is
        // tolerated). A marker embedded in other text (or a missing closing brace) is rejected rather
        // than kept as a literal.
        var match = ReferencePattern().Match(text.Trim());
        if (!match.Success)
        {
            throw new InputCompilationException(
                $"Input '{text}' embeds a '${{...}}' reference marker in surrounding text. A reference must be the whole value, written as ${{step_output(stepId.outputName)}}.");
        }

        return ParseReference(match.Groups[1].Value);
    }

    /// <summary>
    /// Parses the content of a <c>${...}</c> reference, that is the text between the braces already
    /// unwrapped by <see cref="CompileScalar"/>, into a typed <see cref="InputValue"/>. The content
    /// reads as a function call, either <c>step_output(stepId.outputName)</c> or <c>file(path)</c>.
    /// </summary>
    private static InputValue ParseReference(string reference)
    {
        // Error messages echo the value in the ${...} form the author wrote.
        var display = "${" + reference + "}";

        var stepOutputCall = StepOutputCallPattern().Match(reference);
        if (stepOutputCall.Success)
            return ParseStepOutputReference(stepOutputCall.Groups[1].Value, display);

        var fileCall = FileCallPattern().Match(reference);
        if (fileCall.Success)
            return ParseFileReference(fileCall.Groups[1].Value, display);

        throw new InputCompilationException(
            $"Reference '{display}' is not supported. Use ${{step_output(stepId.outputName)}} or ${{file(path)}}.");
    }

    private static InputValue.StepOutputReference ParseStepOutputReference(string argument, string display)
    {
        var parts = StepOutputArgumentPattern().Match(argument);
        if (!parts.Success)
        {
            throw new InputCompilationException(
                $"Reference '{display}' must be of the form ${{step_output(stepId.outputName)}}.");
        }

        return new InputValue.StepOutputReference(parts.Groups[1].Value, parts.Groups[2].Value);
    }

    /// <summary>
    /// Parses the path of a <c>${file(path)}</c> reference. The path is confined to the resources
    /// root: it must be relative and must not contain <c>.</c> or <c>..</c> segments, so the
    /// definition can never escape the configured directory.
    /// </summary>
    private static InputValue.FileReference ParseFileReference(string path, string display)
    {
        if (path.Length == 0 || System.IO.Path.IsPathRooted(path) || HasTraversalSegment(path))
        {
            throw new InputCompilationException(
                $"Reference '{display}' must be of the form ${{file(path)}} with a relative path that does not contain '.' or '..' segments.");
        }

        return new InputValue.FileReference(path);
    }

    private static bool HasTraversalSegment(string path)
    {
        foreach (var segment in path.Split('/', '\\'))
        {
            if (segment is "." or "..")
                return true;
        }

        return false;
    }
}
