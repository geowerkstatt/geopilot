namespace Geopilot.Pipeline;

/// <summary>
/// Represents the result of a single step in a pipeline.
/// </summary>
public class StepResult
{
    /// <summary>
    /// The raw object returned by the step's process. Its public readable properties are the
    /// step's implicit outputs, resolvable by name via <see cref="ExtractProperty(string)"/>. It is
    /// <see langword="null"/> for synthetic results that have no backing process result, for
    /// example a pre-condition status message.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Extracts the value of a public readable property with the given name from <see cref="Result"/>.
    /// Returns <see langword="null"/> when <see cref="Result"/> is <see langword="null"/> (a synthetic
    /// result), and returns the property value itself when that value happens to be <see langword="null"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to extract.</param>
    /// <returns>The value of the requested property, or <see langword="null"/> when there is no backing result.</returns>
    /// <exception cref="ArgumentException">Thrown when the named property is not found or is not readable.</exception>
    public object? ExtractProperty(string propertyName)
    {
        if (this.Result is null)
            return null;

        var prop = this.Result.GetType().GetProperty(propertyName);
        if (prop is null || !prop.CanRead)
        {
            throw new ArgumentException($"Result has no readable property {propertyName}");
        }

        return prop.GetValue(this.Result);
    }
}
