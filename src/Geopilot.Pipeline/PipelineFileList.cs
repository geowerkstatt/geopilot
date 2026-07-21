using Geopilot.PipelineCore.Pipeline;
using System.Collections;

namespace Geopilot.Pipeline;

/// <inheritdoc />
public class PipelineFileList : IPipelineFileList
{
    /// <summary>
    /// Initializes a new instance of the PipelineFiles class with an empty collection of pipeline files.
    /// </summary>
    public PipelineFileList()
    {
        this.Files = new List<IPipelineFile>();
    }

    /// <summary>
    /// Initializes a new instance of the PipelineFiles class with the specified collection of pipeline files.
    /// </summary>
    /// <param name="files">The collection of pipeline files to be managed by the PipelineFiles instance. This collection cannot be null.</param>
    public PipelineFileList(ICollection<IPipelineFile> files)
    {
        this.Files = files;
    }

    /// <inheritdoc />
    public ICollection<IPipelineFile> Files { get; }

    /// <inheritdoc />
    public IEnumerator<IPipelineFile> GetEnumerator() => this.Files.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.Files.GetEnumerator();
}
