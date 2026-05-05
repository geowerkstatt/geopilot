namespace Geopilot.Api.FileAccess;

/// <summary>
/// Pairs a writable stream with the file name it was opened under and disposes the stream on disposal.
/// </summary>
public sealed class FileHandle : IDisposable
{
    /// <summary>
    /// Name of the file without path information.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Stream to the file.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileHandle"/> class.
    /// </summary>
    public FileHandle(string fileName, Stream stream)
    {
        FileName = fileName;
        Stream = stream;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stream.Dispose();
    }
}
