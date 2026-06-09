using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline.Process.Matcher.FileRenamer;

/// <summary>
/// Renames the uploaded files according to the mapping rules configured by <c>fileMappings</c>.
/// Returns the renamed files under <c>renamed_files</c> and any unmatched mapping rules under <c>unmatched_mappings</c>.
/// </summary>
/// <remarks>
/// Files matching multiple mappings are ignored.
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

    private readonly List<FileMapping> fileMappings;
    private readonly IPipelineFileManager pipelineFileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenamerProcess"/> class with the given file mapping configuration.
    /// </summary>
    /// <param name="fileMappings">The list of file mappings to use for renaming files.</param>
    /// <param name="pipelineFileManager">The pipeline file manager to use for managing files.</param>
    public FileRenamerProcess(List<FileMapping> fileMappings, IPipelineFileManager pipelineFileManager)
    {
        this.fileMappings = fileMappings ?? new List<FileMapping>();
        this.pipelineFileManager = pipelineFileManager ?? throw new ArgumentNullException(nameof(pipelineFileManager));
    }

    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync([UploadFiles] IPipelineFileList uploadFiles, CancellationToken cancellationToken)
    {
        var renamedFiles = new List<IPipelineFile>();
        var unmatchedFiles = new List<IPipelineFile>();
        var unmatchedMappings = new List<FileMapping>(fileMappings);

        foreach (var file in uploadFiles.Files)
        {
            var matchedTargetPath = new List<string>();
            foreach (var mapping in fileMappings)
            {
                var match = Regex.Match(file.OriginalFileName, mapping.Pattern);
                if (match.Success)
                {
                    var targetPath = match.Result(mapping.Target);
                    matchedTargetPath.Add(targetPath);
                    unmatchedMappings.Remove(mapping);
                }
            }

            if (matchedTargetPath.Count == 1)
            {
                var targetPath = matchedTargetPath.First();
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
            else
            {
                unmatchedFiles.Add(file);
            }
        }

        var unmatchedFileNames = string.Join(", ", unmatchedFiles.Select(f => f.OriginalFileName));
        var statusMessage = unmatchedFiles.Count == 0
            ? AllMatchStatusMessage
            : StatusMessageFormat.ToDictionary(msg => msg.Key, msg => string.Format(CultureInfo.InvariantCulture, msg.Value, unmatchedFileNames));

        return new Dictionary<string, object?>
        {
            { "renamed_files", renamedFiles },
            { "unmatched_files", unmatchedFiles },
            { "unmatched_mappings", unmatchedMappings },
            { "status_message", statusMessage },
        };
    }
}
