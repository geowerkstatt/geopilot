namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// Represents a collection of pipeline files and provides methods to filter and manipulate the collection based on
/// specified criteria.
/// </summary>
/// <remarks>Implementations of this interface allow users to retrieve, filter, and modify collections of pipeline
/// files using extension filters, name patterns, or custom predicates. The interface is designed to support flexible
/// querying and manipulation of pipeline file sets in scenarios such as build pipelines or data processing
/// workflows.</remarks>
public interface IPipelineFileList
{
    /// <summary>
    /// Gets the collection of files associated with the pipeline.
    /// </summary>
    /// <remarks>This property provides access to the files that are part of the pipeline's processing. The
    /// collection is read-only and reflects the current state of the files managed by the pipeline.</remarks>
    ICollection<IPipelineFile> Files { get; }

    /// <summary>
    /// Filters the pipeline files to include only those with the specified file extensions.
    /// </summary>
    /// <remarks>Use this method to restrict processing to files of certain types, such as when only specific
    /// formats are relevant to a pipeline operation.</remarks>
    /// <param name="extensions">A set of file extensions, each without the leading dot (for example, "txt"), used to determine which files
    /// are included in the result. Cannot be null. Comparison is case-insensitive.</param>
    /// <returns>An instance of IPipelineFiles containing only the files that match the specified extensions.</returns>
    IPipelineFileList WithExtensions(HashSet<string> extensions);

    /// <summary>
    /// Filters the pipeline files to include only those with names matching the specified pattern.
    /// </summary>
    /// <param name="namePattern">The pattern to match file names against. Cannot be null.</param>
    /// <returns>An instance of IPipelineFiles containing only the files that match the specified name pattern.</returns>
    IPipelineFileList WithMatchingName(string namePattern);

    /// <summary>
    /// Filters the pipeline files based on a custom predicate.
    /// </summary>
    /// <param name="predicate">A function that defines the conditions to filter the files. Cannot be null.</param>
    /// <returns>An instance of IPipelineFiles containing only the files that match the specified predicate.</returns>
    IPipelineFileList Matches(Func<IPipelineFile, bool> predicate);
}
