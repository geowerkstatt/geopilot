namespace Geopilot.Api.FileAccess;

/// <summary>
/// The local data directories an installation uses, and the probe that proves them usable at startup. The startup
/// probe and <see cref="StorageHealthCheck"/> both take the list from <see cref="Local"/>, so it reflects the
/// configured storage backend in one place.
/// </summary>
public static class DataDirectories
{
    private const string WriteProbePrefix = ".geopilot-write-probe-";

    /// <summary>
    /// Returns the local directories configured under <see cref="FileAccessOptions.SectionName"/>.
    /// </summary>
    public static IReadOnlyList<DataDirectory> Local(FileAccessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The first two stay local in every mode: the job scratch is ephemeral, the resources ship with the deployment.
        return
        [
            Entry(nameof(FileAccessOptions.PipelineDirectory), options.PipelineDirectory, DirectoryAccess.ReadWrite),
            Entry(nameof(FileAccessOptions.ResourcesDirectory), options.ResourcesDirectory, DirectoryAccess.Read),

            // shortcut: physical artifact stores only; the cloud mode of #850 leaves these out and checks the store instead
            Entry(nameof(FileAccessOptions.DownloadDirectory), options.DownloadDirectory, DirectoryAccess.ReadWrite),
            Entry(nameof(FileAccessOptions.VisualizationDirectory), options.VisualizationDirectory, DirectoryAccess.ReadWrite),
            Entry(nameof(FileAccessOptions.AssetsDirectory), options.AssetsDirectory, DirectoryAccess.ReadWrite),
        ];
    }

    /// <summary>
    /// Creates each directory if it is missing and proves the access it needs: a probe file is written and deleted
    /// for <see cref="DirectoryAccess.ReadWrite"/>, the entries are listed for <see cref="DirectoryAccess.Read"/>.
    /// Meant to run once at startup, so a missing mount or a volume with the wrong owner stops the start instead
    /// of failing the first job.
    /// </summary>
    /// <exception cref="InvalidOperationException">A directory cannot be created or accessed; the message names its key and path.</exception>
    public static void EnsureUsable(IEnumerable<DataDirectory> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);

        foreach (var directory in directories)
        {
            try
            {
                Directory.CreateDirectory(directory.Path);
                if (directory.Access == DirectoryAccess.ReadWrite)
                    WriteProbe(directory.Path);
                else
                    _ = Directory.EnumerateFileSystemEntries(directory.Path).Any();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                var needed = directory.Access == DirectoryAccess.ReadWrite ? "created and written to" : "created and read";
                throw new InvalidOperationException(
                    $"{directory.Key} <{Path.GetFullPath(directory.Path)}> cannot be {needed}: {ex.Message}", ex);
            }
        }
    }

    private static DataDirectory Entry(string propertyName, string path, DirectoryAccess access)
        => new($"{FileAccessOptions.SectionName}:{propertyName}", path, access);

    private static void WriteProbe(string directory)
    {
        var probe = Path.Combine(directory, WriteProbePrefix + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(probe, []);
        File.Delete(probe);
    }
}

/// <summary>
/// What the application needs from a local data directory.
/// </summary>
public enum DirectoryAccess
{
    /// <summary>
    /// The application writes to the directory.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// The application only reads from the directory, which may be mounted read-only.
    /// </summary>
    Read,
}

/// <summary>
/// A local data directory, named by the configuration key that sets it.
/// </summary>
/// <param name="Key">The configuration key, e.g. <c>Storage:DownloadDirectory</c>. Safe to report anonymously.</param>
/// <param name="Path">The configured path. Not to be reported anonymously.</param>
/// <param name="Access">What the application needs from the directory.</param>
public record DataDirectory(string Key, string Path, DirectoryAccess Access);
