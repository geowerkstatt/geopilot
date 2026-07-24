namespace Geopilot.Pipeline;

/// <summary>
/// Centralizes the single reflection step used to read a process result's public output properties.
/// A step's outputs are implicit: every public, readable property of its process result is available
/// to later steps by its name. Input binding, condition expression parameters, and output-action
/// tagging all resolve outputs against the map returned here, so reflection over a result's output
/// properties lives in one place.
/// </summary>
internal static class ProcessResultReflection
{
    /// <summary>
    /// Reads the public, readable properties of <paramref name="result"/> into a case-sensitive map
    /// of property name to value. Returns an empty map when <paramref name="result"/> is
    /// <see langword="null"/> (a synthetic result, such as a pre-condition status message, exposes
    /// no outputs).
    /// </summary>
    /// <param name="result">The raw object returned by a step's process, or <see langword="null"/>.</param>
    /// <returns>A map of property name to value; empty when there is nothing to read.</returns>
    public static IReadOnlyDictionary<string, object?> ReadProperties(object? result)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (result is null)
        {
            return properties;
        }

        foreach (var property in result.GetType().GetProperties())
        {
            // Skip indexers (for example a string's Chars or a list's this[int]): they require index
            // arguments, so GetValue with none throws, and an indexer is never a named step output.
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                properties[property.Name] = property.GetValue(result);
            }
        }

        return properties;
    }
}
