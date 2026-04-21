using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Api.Pipeline.Process.Matcher.FileMatcher;

/// <summary>
/// Filters the uploaded files and returns those matching the configured criteria as <c>matched_files</c>.
/// </summary>
/// <remarks>
/// All configured filters are applied with AND semantics: a file must satisfy every active filter to be included.
/// Within a single filter, multiple values are combined with OR semantics (e.g. extensions "pdf,png" matches either).
/// Filters whose configuration is null or empty are skipped and do not restrict the result.
/// </remarks>
internal class FileMatcherProcess
{
    private readonly HashSet<string> fileExtensions;
    private readonly HashSet<string> fileNamePatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMatcherProcess"/> class with the given filter configuration.
    /// </summary>
    /// <param name="fileExtensions">Set of file extensions to match (e.g. "pdf", "png"). Case-insensitive. Null or empty means no extension filter.</param>
    /// <param name="fileNamePatterns">Set of regex patterns matched against the original file name (e.g. "Road.*", "Map.*"). Null or empty means no name filter.</param>
    public FileMatcherProcess(HashSet<string>? fileExtensions, HashSet<string>? fileNamePatterns)
    {
        this.fileExtensions = fileExtensions ?? new HashSet<string>();
        this.fileNamePatterns = fileNamePatterns ?? new HashSet<string>();
    }

    [PipelineProcessRun]
    public Task<Dictionary<string, object?>> RunAsync([UploadFiles] IPipelineFileList uploadFiles)
    {
        var filtered = uploadFiles;

        // Keep only files whose extension matches any of the configured extensions.
        if (fileExtensions.Count > 0)
            filtered = filtered.WithExtensions(fileExtensions);

        // Keep only files whose name matches any of the configured regex patterns.
        // Multiple patterns are combined into a single alternation: (p1)|(p2)|...
        if (fileNamePatterns.Count > 0)
            filtered = filtered.WithMatchingName(string.Join("|", fileNamePatterns.Select(p => $"({p})")));

        return Task.FromResult(new Dictionary<string, object?>
        {
            { "matched_files", filtered.Files.ToArray() },
        });
    }
}
