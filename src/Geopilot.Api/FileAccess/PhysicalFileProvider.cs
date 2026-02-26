namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides read/write access to files in a predefined folder.
/// </summary>
public class PhysicalFileProvider : IFileProvider
{
    private readonly IDirectoryProvider directoryProvider;

    private DirectoryInfo? homeDirectory;

    private DirectoryInfo HomeDirectory => homeDirectory ?? throw new InvalidOperationException("The file provider needs to be initialized first.");

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalFileProvider"/> at the given root directory path.
    /// </summary>
    /// <param name="directoryProvider">The directory provider.</param>
    public PhysicalFileProvider(IDirectoryProvider directoryProvider)
    {
        this.directoryProvider = directoryProvider;
    }

    /// <inheritdoc/>
    public Stream CreateFile(string file)
    {
        return File.Create(Path.Combine(HomeDirectory.FullName, file));
    }

    /// <inheritdoc/>
    public FileHandle CreateFileWithRandomName(string extension)
    {
        var fileName = Path.ChangeExtension(Path.GetRandomFileName(), extension);
        var stream = CreateFile(fileName);

        return new FileHandle(fileName, stream);
    }

    /// <inheritdoc/>
    public Stream Open(string file)
    {
        return File.OpenRead(Path.Combine(HomeDirectory.FullName, file));
    }

    /// <inheritdoc/>
    public bool Exists(string file)
    {
        return File.Exists(Path.Combine(HomeDirectory.FullName, file.SanitizeFileName()));
    }

    /// <inheritdoc/>
    public virtual IEnumerable<string> GetFiles()
    {
        return HomeDirectory.GetFiles().Select(x => x.Name);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">If <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    public void Initialize(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("The specified id is not valid.", nameof(id));
        homeDirectory = Directory.CreateDirectory(directoryProvider.GetUploadDirectoryPath(id));
    }

    /// <inheritdoc/>
    public string? GetFilePath(string file)
    {
        var filePath = Path.Combine(HomeDirectory.FullName, file);
        return File.Exists(filePath) ? filePath : null;
    }
}
