using Geopilot.PipelineCore.Pipeline;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline;

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
    public IPipelineFileList Matches(Func<IPipelineFile, bool> predicate)
    {
        return new PipelineFileList(this.Files.Where(predicate).ToList());
    }

    /// <inheritdoc />
    public IPipelineFileList WithExtensions(HashSet<string> extensions)
    {
        return Matches(file => extensions.Select(e => e.ToLowerInvariant()).Contains(file.FileExtension.ToLowerInvariant()));
    }

    /// <inheritdoc />
    public IPipelineFileList WithMatchingName(string namePattern)
    {
        return Matches(file => Regex.IsMatch(file.OriginalFileName, namePattern));
    }
}
