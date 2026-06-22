using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

/// <summary>
/// Wraps an <see cref="IPipelineFile"/> that a step received as input (from another step or from the
/// upload) so the consuming step can read it freely but never mutates the original. The first time a
/// writable local path is requested via <see cref="GetLocalPath"/>, the content is copied into the
/// consuming step's working directory and all further access uses that private copy. This is how
/// geopilot guarantees that process inputs are isolated, without the process copying anything itself.
/// </summary>
internal sealed class CopyOnWriteFile : IPipelineFile
{
    private readonly IPipelineFile inner;
    private readonly string pipelineDirectory;
    private readonly string stepId;
    private IPipelineFile? localCopy;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopyOnWriteFile"/> class.
    /// </summary>
    /// <param name="inner">The input file, owned by another step or the upload.</param>
    /// <param name="pipelineDirectory">The pipeline working directory (parent of the per-step directories).</param>
    /// <param name="stepId">The id of the consuming step, used as its working directory name.</param>
    public CopyOnWriteFile(IPipelineFile inner, string pipelineDirectory, string stepId)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.pipelineDirectory = pipelineDirectory ?? throw new ArgumentNullException(nameof(pipelineDirectory));
        this.stepId = stepId ?? throw new ArgumentNullException(nameof(stepId));
    }

    /// <inheritdoc/>
    public string OriginalFileName => this.inner.OriginalFileName;

    /// <inheritdoc/>
    public string OriginalFileNameWithoutExtension => this.inner.OriginalFileNameWithoutExtension;

    /// <inheritdoc/>
    public string FileExtension => this.inner.FileExtension;

    /// <inheritdoc/>
    public string OriginalRelativePath => this.inner.OriginalRelativePath;

    /// <summary>
    /// Gets the private copy once it has been materialized, otherwise the wrapped input.
    /// </summary>
    private IPipelineFile Current => this.localCopy ?? this.inner;

    /// <inheritdoc/>
    public FileStream OpenReadFileStream() => this.Current.OpenReadFileStream();

    /// <inheritdoc/>
    public string GetLocalPath() => this.EnsureLocalCopy().GetLocalPath();

    /// <inheritdoc/>
    public FileStream OpenWriteFileStream()
        => throw new NotSupportedException(
            "Cannot stream-write an input file. Read it via OpenReadFileStream, mutate it in place via GetLocalPath, or create a new output with IPipelineFileManager.GeneratePipelineFile.");

    /// <summary>
    /// Materializes the private copy in the consuming step's working directory on first use.
    /// </summary>
    /// <returns>The private, owned copy of the wrapped input.</returns>
    private IPipelineFile EnsureLocalCopy()
    {
        if (this.localCopy is null)
        {
            var stepFileManager = new PipelineFileManager(this.pipelineDirectory, this.stepId);
            this.localCopy = stepFileManager.CreateWritableCopy(this.inner, this.inner.OriginalFileNameWithoutExtension);
        }

        return this.localCopy;
    }
}
