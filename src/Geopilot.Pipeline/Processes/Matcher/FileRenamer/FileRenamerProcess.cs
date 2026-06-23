using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Processes.Matcher.FileRenamer;

/// <summary>
/// Renames the uploaded files according to the mapping rules configured by <c>fileMappings</c>.
/// Returns the renamed files under <c>renamed_files</c>, files that matched no or several mappings under
/// <c>unmatched_files</c>, files whose resolved target path collides with another file's under
/// <c>conflicting_files</c>, and mapping rules that matched no file under <c>unmatched_mappings</c>.
/// </summary>
/// <remarks>
/// A file matching multiple mappings is treated as unmatched. When two or more files resolve to the same
/// target path, none of them is renamed (the rename would later collide in the worker mount); those files
/// are reported under <c>conflicting_files</c> instead.
/// </remarks>
internal class FileRenamerProcess
{
    private static readonly Dictionary<string, string> StatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Folgende Datei(en) entsprechen keinen Kriterien: {0}" },
        { "fr", "Les fichiers suivants ne correspondent à aucun critère : {0}" },
        { "it", "I seguenti file non corrispondono a nessun criterio: {0}" },
        { "en", "The following file(s) do not match any criteria: {0}" },
    };

    private static readonly Dictionary<string, string> AllMatchStatusMessage = new Dictionary<string, string>
    {
        { "de", "Alle Dateien entsprechen den Kriterien." },
        { "fr", "Tous les fichiers correspondent aux critères." },
        { "it", "Tutti i file corrispondono ai criteri." },
        { "en", "All files match the criteria." },
    };

    private static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromSeconds(5);

    private readonly List<CompiledMapping> compiledMappings;
    private readonly IPipelineFileManager pipelineFileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenamerProcess"/> class with the given file mapping configuration.
    /// </summary>
    /// <param name="fileMappings">The list of file mappings to use for renaming files.</param>
    /// <param name="pipelineFileManager">The pipeline file manager to use for managing files.</param>
    /// <param name="regexTimeoutMilliseconds">Per-match timeout for the configured patterns, guarding against catastrophic backtracking on adversarial file names. Defaults to 5 seconds when null or not positive.</param>
    public FileRenamerProcess(List<FileMapping> fileMappings, IPipelineFileManager pipelineFileManager, int? regexTimeoutMilliseconds = null)
    {
        this.pipelineFileManager = pipelineFileManager ?? throw new ArgumentNullException(nameof(pipelineFileManager));

        var matchTimeout = regexTimeoutMilliseconds is > 0
            ? TimeSpan.FromMilliseconds(regexTimeoutMilliseconds.Value)
            : DefaultRegexTimeout;
        compiledMappings = (fileMappings ?? new List<FileMapping>())
            .Select(mapping => new CompiledMapping(mapping, new Regex(mapping.Pattern, RegexOptions.None, matchTimeout)))
            .ToList();
    }

    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync([UploadFiles] IPipelineFileList uploadFiles, CancellationToken cancellationToken)
    {
        var unmatchedFiles = new List<IPipelineFile>();
        var unmatchedMappings = compiledMappings.Select(compiled => compiled.Mapping).ToList();
        var resolvedTargets = new List<(IPipelineFile File, string TargetPath)>();

        foreach (var file in uploadFiles.Files)
        {
            var matchedTargetPaths = new List<string>();
            foreach (var compiled in compiledMappings)
            {
                var match = compiled.Regex.Match(file.OriginalFileName);
                if (match.Success)
                {
                    matchedTargetPaths.Add(match.Result(compiled.Mapping.Target));
                    unmatchedMappings.Remove(compiled.Mapping);
                }
            }

            if (matchedTargetPaths.Count == 1)
                resolvedTargets.Add((file, matchedTargetPaths[0]));
            else
                unmatchedFiles.Add(file);
        }

        // A target claimed by more than one file cannot be produced unambiguously; renaming both would
        // collide later in the worker mount (FileMode.CreateNew). Such files are reported, not renamed.
        var fileCountPerTarget = resolvedTargets
            .GroupBy(entry => entry.TargetPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var renamedFiles = new List<IPipelineFile>();
        var conflictingFiles = new List<IPipelineFile>();
        foreach (var (file, targetPath) in resolvedTargets)
        {
            if (fileCountPerTarget[targetPath] > 1)
            {
                conflictingFiles.Add(file);
                continue;
            }

            var directory = Path.GetDirectoryName(targetPath);
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
            var extension = Path.GetExtension(targetPath).TrimStart('.');
            var renamedFile = string.IsNullOrEmpty(directory)
                ? pipelineFileManager.GeneratePipelineFile(filenameWithoutExtension, extension)
                : pipelineFileManager.GeneratePipelineFile(directory, filenameWithoutExtension, extension);

            await using var sourceStream = file.OpenReadFileStream();
            await using var targetStream = renamedFile.OpenWriteFileStream();
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
            await targetStream.FlushAsync(cancellationToken);

            renamedFiles.Add(renamedFile);
        }

        var unmatchedFileNames = string.Join(", ", unmatchedFiles.Select(f => f.OriginalFileName));
        var statusMessage = unmatchedFiles.Count == 0
            ? AllMatchStatusMessage
            : StatusMessageFormat.ToDictionary(msg => msg.Key, msg => string.Format(CultureInfo.InvariantCulture, msg.Value, unmatchedFileNames));

        return new Dictionary<string, object?>
        {
            { "renamed_files", renamedFiles },
            { "unmatched_files", unmatchedFiles },
            { "conflicting_files", conflictingFiles },
            { "unmatched_mappings", unmatchedMappings },
            { "status_message", statusMessage },
        };
    }

    private sealed record CompiledMapping(FileMapping Mapping, Regex Regex);
}
