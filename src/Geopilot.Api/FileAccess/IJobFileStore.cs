namespace Geopilot.Api.FileAccess;

/// <summary>
/// Common shape shared by per-job file stores. Implementations are stateless — every
/// operation takes the job id explicitly and resolves it to a per-job directory under
/// the store's configured root. File names are sanitized internally.
/// </summary>
public interface IJobFileStore
{
    /// <summary>
    /// Creates or overwrites the file with the given <paramref name="fileName"/> in
    /// the job's directory and returns a writable stream.
    /// </summary>
    Stream CreateFile(Guid jobId, string fileName);

    /// <summary>
    /// Opens the file with the given <paramref name="fileName"/> in the job's
    /// directory for reading.
    /// </summary>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    Stream OpenFile(Guid jobId, string fileName);

    /// <summary>
    /// Returns whether a file with the given <paramref name="fileName"/> exists in
    /// the job's directory.
    /// </summary>
    bool Exists(Guid jobId, string fileName);

    /// <summary>
    /// Returns the absolute path the file with the given <paramref name="fileName"/>
    /// would have inside the job's directory. Does not check existence.
    /// </summary>
    string GetPath(Guid jobId, string fileName);

    /// <summary>
    /// Enumerates the file names in the job's directory. Returns an empty sequence
    /// if the directory does not exist.
    /// </summary>
    IEnumerable<string> ListFiles(Guid jobId);

    /// <summary>
    /// Deletes the entire job directory if it exists.
    /// </summary>
    void DeleteJob(Guid jobId);
}
