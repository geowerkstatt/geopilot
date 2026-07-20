namespace Geopilot.Pipeline.Config;

/// <summary>
/// A compiled pipeline step input value for a single process parameter. Produced by
/// <see cref="InputCompiler"/> from the raw YAML input map and consumed by the runtime when it
/// binds process parameters. A value is either a literal written in the definition or a reference
/// that is resolved when the step runs.
/// </summary>
public abstract record InputValue
{
    /// <summary>
    /// A scalar written literally in the definition. The value is kept as text because the
    /// conversion to the target parameter type happens later, during binding.
    /// <see langword="null"/> represents an explicit YAML null.
    /// </summary>
    public sealed record Literal(string? Raw) : InputValue;

    /// <summary>
    /// A reference to a named output of an earlier step, written in the definition as
    /// <c>${step_output(stepId.outputName)}</c>.
    /// </summary>
    public sealed record StepOutputReference(string StepId, string OutputName) : InputValue;

    /// <summary>
    /// A list written as a YAML sequence in the definition. Each item is a single value, either a
    /// literal or a step output reference. Because nested lists are not supported, an item is never
    /// itself a <see cref="Sequence"/>.
    /// </summary>
    public sealed record Sequence(IReadOnlyList<InputValue> Items) : InputValue;

    /// <summary>
    /// A reference to a file provided from the configured resources directory, written in the
    /// definition as <c>${file(relativePath)}</c>. The path is relative to the resources root and is
    /// resolved to an IPipelineFile when the step runs.
    /// </summary>
    public sealed record FileReference(string RelativePath) : InputValue;
}
