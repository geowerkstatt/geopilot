namespace Geopilot.Pipeline.Config;

/// <summary>
/// A compiled pipeline step input value for a single process parameter. Produced by
/// <see cref="InputCompiler"/> from the raw YAML input map and consumed by the runtime when it
/// binds process parameters. A value is either a literal written in the definition or a reference
/// that is resolved when the step runs.
/// </summary>
internal abstract record InputValue
{
    /// <summary>
    /// A scalar written literally in the definition. The value is kept as text because the
    /// conversion to the target parameter type happens later, during binding.
    /// <see langword="null"/> represents an explicit YAML null.
    /// </summary>
    internal sealed record Literal(string? Raw) : InputValue;

    /// <summary>
    /// A reference to a named output of an earlier step, written in the definition as
    /// <c>${step_output(stepId.outputName)}</c>.
    /// </summary>
    internal sealed record StepOutputReference(string StepId, string OutputName) : InputValue;
}
