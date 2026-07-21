namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// Represents a collection of pipeline files and provides methods to filter and manipulate the collection based on
/// specified criteria.
/// </summary>
/// <remarks>Implementations of this interface allow users to retrieve, filter, and modify collections of pipeline
/// files using extension filters, name patterns, or custom predicates. The interface is designed to support flexible
/// querying and manipulation of pipeline file sets in scenarios such as build pipelines or data processing
/// workflows.</remarks>
public interface IPipelineFileList : IEnumerable<IPipelineFile>
{
    /// <summary>
    /// Gets the collection of files associated with the pipeline.
    /// </summary>
    /// <remarks>This property provides access to the files that are part of the pipeline's processing. The
    /// collection is read-only and reflects the current state of the files managed by the pipeline.</remarks>
    ICollection<IPipelineFile> Files { get; }
}
