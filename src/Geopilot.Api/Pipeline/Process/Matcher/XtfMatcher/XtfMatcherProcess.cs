using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Xml.Linq;

namespace Geopilot.Api.Pipeline.Process.Matcher.XtfMatcher;

/// <summary>
/// Filters the uploaded files and returns those matching the configured criteria as <c>xtf_files</c>.
/// </summary>
/// <remarks>
/// All configured filters are applied with AND semantics: a file must satisfy every active filter to be included.
/// Within a single filter, multiple values are combined with OR semantics (e.g. extensions "xtf,itf" matches either).
/// Filters whose configuration is null or empty are skipped and do not restrict the result.
/// </remarks>
internal class XtfMatcherProcess
{
    private readonly HashSet<string> fileExtensions;
    private readonly HashSet<string> iliModels;
    private readonly HashSet<string> fileNamePatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtfMatcherProcess"/> class with the given filter configuration.
    /// </summary>
    /// <param name="fileExtensions">Comma-separated list of file extensions to match (e.g. "xtf,itf"). Case-insensitive. Null or empty means no extension filter.</param>
    /// <param name="iliModels">Comma-separated list of ILI model names to match against the <c>ili:model</c> elements in the XTF header. Null or empty means no model filter.</param>
    /// <param name="fileNamePatterns">Comma-separated list of regex patterns matched against the original file name (e.g. "Road.*,Map.*"). Null or empty means no name filter.</param>
    public XtfMatcherProcess(string? fileExtensions, string? iliModels, string? fileNamePatterns)
    {
        this.fileExtensions = ParseCommaSeparatedConfiguration(fileExtensions);
        this.iliModels = ParseCommaSeparatedConfiguration(iliModels);
        this.fileNamePatterns = ParseCommaSeparatedConfiguration(fileNamePatterns);
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

        // Keep only files that declare at least one of the configured ILI models in their XTF header.
        // Files that cannot be parsed as XTF are excluded.
        if (iliModels.Count > 0)
            filtered = filtered.Matches(file => ExtractIliModels(file).Overlaps(iliModels));

        return Task.FromResult(new Dictionary<string, object?>
        {
            { "xtf_files", filtered.Files.ToArray() },
        });
    }

    /// <summary>
    /// Reads the ILI model names declared in the XTF header of the given file.
    /// Returns the values of all <c>ili:transfer/ili:headersection/ili:models/ili:model</c> elements.
    /// The namespace bound to the <c>ili</c> prefix is resolved dynamically from the document root,
    /// so any <c>xmlns:ili</c> attribute value is supported.
    /// Returns an empty set if the file cannot be parsed as valid XTF or has no <c>ili</c> prefix declared.
    /// </summary>
    private static HashSet<string> ExtractIliModels(IPipelineFile file)
    {
        try
        {
            using var stream = file.OpenReadFileStream();
            var doc = XDocument.Load(stream);
            var root = doc.Root;
            var iliNamespace = root?.GetNamespaceOfPrefix("ili");
            if (root == null || iliNamespace == null)
                return new HashSet<string>();

            return root
                .Element(iliNamespace + "headersection")?
                .Element(iliNamespace + "models")?
                .Elements(iliNamespace + "model")
                .Select(e => e.Value)
                .ToHashSet() ?? new HashSet<string>();
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    private static HashSet<string> ParseCommaSeparatedConfiguration(string? commaSeparatedValues)
    {
        if (string.IsNullOrEmpty(commaSeparatedValues))
            return new HashSet<string>();
        else
            return commaSeparatedValues.Split(',').ToHashSet();
    }
}
