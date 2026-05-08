namespace Geopilot.Api.FileAccess;

/// <summary>
/// Local-filesystem implementation of <see cref="IJobFileStore"/>. Each operation
/// is sanitized and resolved to <c>{rootDirectory}/{jobId}/{fileName}</c>.
/// </summary>
public abstract class PhysicalJobFileStore : IJobFileStore
{
    private readonly Func<Guid, string> resolveJobDirectoryPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalJobFileStore"/> class.
    /// </summary>
    /// <param name="resolveJobDirectoryPath">Function that maps a job id to the absolute directory path for that job.</param>
    protected PhysicalJobFileStore(Func<Guid, string> resolveJobDirectoryPath)
    {
        this.resolveJobDirectoryPath = resolveJobDirectoryPath;
    }

    /// <inheritdoc/>
    public Stream CreateFile(Guid jobId, string fileName)
    {
        var directory = resolveJobDirectoryPath(jobId);
        Directory.CreateDirectory(directory);
        return File.Create(Path.Combine(directory, fileName.SanitizeFileName()));
    }

    /// <inheritdoc/>
    public Stream OpenFile(Guid jobId, string fileName)
        => File.OpenRead(Path.Combine(resolveJobDirectoryPath(jobId), fileName.SanitizeFileName()));

    /// <inheritdoc/>
    public bool Exists(Guid jobId, string fileName)
        => File.Exists(Path.Combine(resolveJobDirectoryPath(jobId), fileName.SanitizeFileName()));

    /// <inheritdoc/>
    public string GetPath(Guid jobId, string fileName)
        => Path.Combine(resolveJobDirectoryPath(jobId), fileName.SanitizeFileName());

    /// <inheritdoc/>
    public IEnumerable<string> ListFiles(Guid jobId)
    {
        var directory = resolveJobDirectoryPath(jobId);
        if (!Directory.Exists(directory))
            return Array.Empty<string>();
        return new DirectoryInfo(directory).GetFiles().Select(f => f.Name);
    }

    /// <inheritdoc/>
    public void DeleteJob(Guid jobId)
    {
        var directory = resolveJobDirectoryPath(jobId);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
