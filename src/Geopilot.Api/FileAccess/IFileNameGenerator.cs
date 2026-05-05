namespace Geopilot.Api.FileAccess;

/// <summary>
/// Generates file names for files written into the file stores. Extracted from the file
/// stores so tests can inject a deterministic name generator.
/// </summary>
public interface IFileNameGenerator
{
    /// <summary>
    /// Generates a fresh random file name with the specified <paramref name="extension"/>.
    /// </summary>
    /// <param name="extension">Extension to apply to the generated name. May or may not include a leading dot.</param>
    string CreateRandomName(string extension);
}
