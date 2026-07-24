namespace Geopilot.Pipeline;

/// <summary>
/// Represents the result of a single step in a pipeline.
/// </summary>
public class StepResult
{
    /// <summary>
    /// The raw object returned by the step's process. Its public readable properties are the
    /// step's implicit outputs, resolvable by name via <see cref="TryGetOutput"/>. It is
    /// <see langword="null"/> for synthetic results that have no backing process result, for
    /// example a pre-condition status message.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// The outputs this step exposes for handling: the process result properties tagged with an
    /// action (via <c>output_actions</c>) plus any synthesized status messages, keyed by the
    /// result property name.
    /// </summary>
    public Dictionary<string, StepOutput> ActionOutputs { get; set; } = new Dictionary<string, StepOutput>();

    /// <summary>
    /// Resolves an implicit output by its result property name, reading it from <see cref="Result"/>
    /// via reflection. Returns <see langword="false"/> when there is no readable property of that name.
    /// </summary>
    /// <param name="name">The PascalCase name of the result property.</param>
    /// <param name="value">The resolved value when found; otherwise <see langword="null"/>.</param>
    public bool TryGetOutput(string name, out object? value)
        => ProcessResultReflection.ReadProperties(Result).TryGetValue(name, out value);
}
