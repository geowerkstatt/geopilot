using Geopilot.Api.Services;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// An <see cref="IPipelineFile"/> backed by an uploaded file in cloud storage. The content is fetched into
/// the job's pipeline working directory on first access and reused from there, so an uploaded file that no
/// step reads is not fetched during the run and a job starts without waiting for the whole upload.
/// Declaring a delivery archives every uploaded file afterwards, including those, so this saves the
/// transfer only for a job that is never delivered.
/// Steps see this file through the runtime's copy-on-write view, so the fetched copy is never modified.
/// </summary>
internal sealed class CloudPipelineFile : IPipelineFile
{
    private readonly ICloudStorageService cloudStorageService;
    private readonly string cloudKey;
    private readonly string materializationDirectory;
    private readonly string materializedFileName;
    private readonly object materializeGate = new();
    private Task<string>? materializeTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudPipelineFile"/> class.
    /// </summary>
    /// <param name="cloudStorageService">The store the file is fetched from.</param>
    /// <param name="cloudKey">The storage key of the uploaded file.</param>
    /// <param name="originalFileName">The file name the user uploaded, including its extension.</param>
    /// <param name="materializationDirectory">The directory the file is fetched into on first access.</param>
    /// <param name="materializedFileName">The on-disk name inside <paramref name="materializationDirectory"/>, unique within the job.</param>
    public CloudPipelineFile(
        ICloudStorageService cloudStorageService,
        string cloudKey,
        string originalFileName,
        string materializationDirectory,
        string materializedFileName)
    {
        ArgumentNullException.ThrowIfNull(cloudStorageService);
        ArgumentException.ThrowIfNullOrEmpty(cloudKey);
        ArgumentException.ThrowIfNullOrEmpty(originalFileName);
        ArgumentException.ThrowIfNullOrEmpty(materializationDirectory);
        ArgumentException.ThrowIfNullOrEmpty(materializedFileName);

        this.cloudStorageService = cloudStorageService;
        this.cloudKey = cloudKey;
        this.materializationDirectory = materializationDirectory;
        this.materializedFileName = materializedFileName;
        OriginalFileName = originalFileName;
    }

    /// <inheritdoc/>
    public string OriginalFileName { get; }

    /// <inheritdoc/>
    public string OriginalRelativePath => string.Empty;

    /// <inheritdoc/>
    public string OriginalFileNameWithoutExtension => Path.GetFileNameWithoutExtension(OriginalFileName);

    /// <inheritdoc/>
    public string FileExtension => Path.GetExtension(OriginalFileName).TrimStart('.');

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        => File.OpenRead(await MaterializeAsync(cancellationToken));

    /// <inheritdoc/>
    public Task<string> GetLocalPathAsync(CancellationToken cancellationToken = default)
        => MaterializeAsync(cancellationToken);

    /// <inheritdoc/>
    public FileStream OpenWriteFileStream()
        => throw new NotSupportedException("Cannot write an uploaded file. Create a new output with IPipelineFileManager.GeneratePipelineFile.");

    /// <summary>
    /// Fetches the file on first access and hands every further caller the same copy. A failed fetch is not
    /// retried: the failure surfaces on every access, so the step that needs the file fails instead of
    /// silently working on an incomplete upload.
    /// </summary>
    private Task<string> MaterializeAsync(CancellationToken cancellationToken)
    {
        lock (materializeGate)
        {
            return materializeTask ??= FetchAsync(cancellationToken);
        }
    }

    private async Task<string> FetchAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(materializationDirectory);
        var path = Path.Combine(materializationDirectory, materializedFileName);

        try
        {
            using var target = File.Create(path);
            await cloudStorageService.DownloadAsync(cloudKey, target, cancellationToken);
        }
        catch
        {
            // A partial file would look like a complete one to the next reader.
            TryDeletePartialFile(path);
            throw;
        }

        return path;
    }

    private static void TryDeletePartialFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
