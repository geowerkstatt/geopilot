using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;
using System.IO.Compression;

namespace Geopilot.Api.Pipeline.Process.Unzip;

/// <summary>
/// Pipeline process that extracts the contents of a single ZIP file and exposes the
/// extracted files as an array of <see cref="IPipelineFile"/> under the output key
/// <c>extracted_files</c>. Directory entries inside the archive are skipped, and
/// nested paths are flattened (only the leaf file name is preserved). A localized
/// status message is exposed under <c>status_message</c>.
/// </summary>
internal class UnzipProcess
{
    private const string OutputMappingExtractedFiles = "extracted_files";
    private const string OutputMappingStatusMessage = "status_message";

    private static readonly Dictionary<string, string> SuccessfulStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "{0} Datei(en) aus dem ZIP Archiv entpackt." },
        { "fr", "{0} fichier(s) extrait(s) de l'archive ZIP." },
        { "it", "{0} file estratti dall'archivio ZIP." },
        { "en", "{0} file(s) extracted from the ZIP archive." },
    };

    private static readonly Dictionary<string, string> EmptyArchiveStatusMessage = new Dictionary<string, string>
    {
        { "de", "Das ZIP Archiv enthält keine Dateien." },
        { "fr", "L'archive ZIP ne contient aucun fichier." },
        { "it", "L'archivio ZIP non contiene file." },
        { "en", "The ZIP archive contains no files." },
    };

    private readonly ILogger logger;
    private readonly IPipelineFileManager pipelineFileManager;

    /// <summary>
    /// Creates a new instance of the <see cref="UnzipProcess"/> class.
    /// </summary>
    /// <param name="pipelineFileManager">The pipeline file manager used to allocate output files for each extracted entry.</param>
    /// <param name="logger">Logger instance for logging messages during extraction.</param>
    public UnzipProcess(IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;
    }

    /// <summary>
    /// Extracts every file entry from the supplied ZIP archive and returns them as an array of
    /// <see cref="IPipelineFile"/> under the <c>extracted_files</c> output key. The original
    /// directory of each entry is preserved on <see cref="IPipelineFile.OriginalRelativePath"/>;
    /// downstream consumers that need a flat view simply ignore that property.
    /// </summary>
    /// <param name="zipFile">The ZIP archive to extract. Cannot be null.</param>
    /// <returns>A dictionary containing the extracted files and a localized status message.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile zipFile)
    {
        var extracted = new List<IPipelineFile>();

        using (var zipStream = zipFile.OpenReadFileStream())
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false))
        {
            foreach (var entry in archive.Entries)
            {
                // ZipArchiveEntry.Name is empty for directory entries (FullName ending in '/').
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var nameWithoutExtension = Path.GetFileNameWithoutExtension(entry.Name);
                var extension = Path.GetExtension(entry.Name).TrimStart('.');

                // Use forward slashes per ZIP convention. The file manager normalizes the
                // path and rejects '..'/rooted entries, so it is the canonical zip-slip boundary.
                var entryDirectory = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? string.Empty;
                var outputFile = pipelineFileManager.GeneratePipelineFile(entryDirectory, nameWithoutExtension, extension);

                using (var entryStream = entry.Open())
                using (var outputStream = outputFile.OpenWriteFileStream())
                {
                    await entryStream.CopyToAsync(outputStream);
                }

                extracted.Add(outputFile);
            }
        }

        Dictionary<string, string> statusMessage;
        if (extracted.Count == 0)
        {
            statusMessage = EmptyArchiveStatusMessage;
            logger.LogWarning("UnzipProcess: ZIP archive '{Archive}' contained no files.", zipFile.OriginalFileName);
        }
        else
        {
            statusMessage = SuccessfulStatusMessageFormat
                .ToDictionary(msg => msg.Key, msg => string.Format(CultureInfo.InvariantCulture, msg.Value, extracted.Count));
            logger.LogInformation("UnzipProcess: extracted {Count} file(s) from '{Archive}'.", extracted.Count, zipFile.OriginalFileName);
        }

        return new Dictionary<string, object?>
        {
            { OutputMappingExtractedFiles, extracted.ToArray() },
            { OutputMappingStatusMessage, statusMessage },
        };
    }
}
