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
    /// <param name="fileExtensions">Set of file extensions to match (e.g. "xtf", "itf"). Case-insensitive. Null or empty means no extension filter.</param>
    /// <param name="iliModels">Set of ILI model names to match against the <c>ili:model</c> elements in the XTF header. Null or empty means no model filter.</param>
    /// <param name="fileNamePatterns">Set of regex patterns matched against the original file name (e.g. "Road.*", "Map.*"). Null or empty means no name filter.</param>
    public XtfMatcherProcess(HashSet<string>? fileExtensions, HashSet<string>? iliModels, HashSet<string>? fileNamePatterns)
    {
        this.fileExtensions = fileExtensions ?? new HashSet<string>();
        this.iliModels = iliModels != null
            ? new HashSet<string>(iliModels, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        // Keep only files that declare at least one of the configured ILI models in their XTF header.
        // Files that cannot be parsed as XTF are excluded.
        if (iliModels.Count > 0)
            filtered = filtered.Matches(file => iliModels.Overlaps(ExtractIliModels(file)));

        return Task.FromResult(new Dictionary<string, object?>
        {
            { "xtf_files", filtered.Files.ToArray() },
        });
    }

    /// <summary>
    /// Reads the ILI model names declared in the XTF header of the given file.
    /// Supports two XTF formats:
    /// <list type="bullet">
    /// <item>INTERLIS 2.4: <c>ili:transfer/ili:headersection/ili:models/ili:model</c> with model name as element text.</item>
    /// <item>INTERLIS 2.3: <c>TRANSFER/HEADERSECTION/MODELS/MODEL</c> (default namespace, uppercase) with model name in the <c>NAME</c> attribute.</item>
    /// </list>
    /// Returns an empty set if the file cannot be parsed or matches neither format.
    /// </summary>
    private static HashSet<string> ExtractIliModels(IPipelineFile file)
    {
        try
        {
            using var stream = file.OpenReadFileStream();
            var doc = XDocument.Load(stream);
            var root = doc.Root;
            if (root == null)
                return new HashSet<string>();

            // INTERLIS 2.4: elements use the ili: prefix, model name is element text.
            var iliNamespace = root.GetNamespaceOfPrefix("ili");
            if (iliNamespace != null)
            {
                return root
                    .Element(iliNamespace + "headersection")?
                    .Element(iliNamespace + "models")?
                    .Elements(iliNamespace + "model")
                    .Select(e => e.Value)
                    .ToHashSet() ?? new HashSet<string>();
            }

            // INTERLIS 2.3: unprefixed uppercase elements in the default namespace, model name is the NAME attribute.
            var defaultNamespace = root.GetDefaultNamespace();
            return root
                .Element(defaultNamespace + "HEADERSECTION")?
                .Element(defaultNamespace + "MODELS")?
                .Elements(defaultNamespace + "MODEL")
                .Select(e => e.Attribute("NAME")?.Value)
                .OfType<string>()
                .ToHashSet() ?? new HashSet<string>();
        }
        catch
        {
            return new HashSet<string>();
        }
    }
}
