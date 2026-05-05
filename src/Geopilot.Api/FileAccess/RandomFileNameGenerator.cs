namespace Geopilot.Api.FileAccess;

/// <inheritdoc />
public class RandomFileNameGenerator : IFileNameGenerator
{
    /// <inheritdoc />
    public string CreateRandomName(string extension)
        => Path.ChangeExtension(Path.GetRandomFileName(), extension);
}
