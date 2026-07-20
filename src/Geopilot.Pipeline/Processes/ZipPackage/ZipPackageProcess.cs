using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO.Compression;

namespace Geopilot.Pipeline.Processes.ZipPackage;

/// <summary>
/// Represents a pipeline process that handles packaging <see cref="IPipelineFile"/> to a ZIP file which is also provided in a <see cref="IPipelineFile"/>.
/// </summary>
/// <remarks>This class is intended for use within a data processing pipeline where ZIP package handling of <see cref="IPipelineFile"/> is required.
/// All non-null <see cref="IPipelineFile"/> provided in the input will be included in the created ZIP archive. The resulting ZIP file is then made available as an output of the process.
/// The ZIP archive is returned as the <see cref="ZipPackageResult.ZipPackage"/> output. If no valid input files are provided, null is returned.</remarks>
internal class ZipPackageProcess
{
    private const string DefaultArchiveFileName = "archive";

    private static readonly LocalizedText SuccessfulStatusMessageFormat = new Dictionary<string, string>
        {
            { "de", "ZIP Paket mit {0} Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant {0} fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente {0} file." },
            { "en", "ZIP package containing {0} file(s) created." },
        };

    private static readonly LocalizedText NoFilesStatusMessage = new Dictionary<string, string>
        {
            { "de", "ZIP Archiv nicht erstellt, keine gültigen Eingabedateien gefunden." },
            { "fr", "Archive ZIP non créée, aucun fichier d'entrée valide trouvé." },
            { "it", "Archivio ZIP non creato, nessun file di input valido trovato." },
            { "en", "ZIP archive not created, no valid input files found." },
        };

    private ILogger logger;

    private string archiveFileName;
    private bool includeUploadFiles;
    private IPipelineFileManager pipelineFileManager;

    /// <summary>
    /// Creates a new instance of the <see cref="ZipPackageProcess"/> class with the specified configuration settings.
    /// </summary>
    /// <param name="archiveFileName">The ZIP file name to use for the output archive without file extension. If null, the default name 'archive' will be used.</param>
    /// <param name="includeUploadFiles">When true, the uploaded files from the pipeline context are included in the ZIP archive. Defaults to false.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the ZIP packaging process.</param>
    /// <param name="logger">Logger instance for logging messages during the initialization process.</param>
    public ZipPackageProcess(string? archiveFileName, bool? includeUploadFiles, IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        this.logger = logger;
        if (!string.IsNullOrEmpty(archiveFileName))
            this.archiveFileName = archiveFileName;
        else
            this.archiveFileName = DefaultArchiveFileName;
        this.includeUploadFiles = includeUploadFiles ?? false;
        this.pipelineFileManager = pipelineFileManager;
    }

    /// <summary>
    /// Creates a ZIP archive containing the specified input files and returns it as a
    /// <see cref="ZipPackageResult"/>.
    /// </summary>
    /// <param name="uploadFiles">Optional uploaded files to include in the ZIP archive. Injected from the pipeline context when the parameter is bound.</param>
    /// <param name="input">An array of input files to include in the ZIP archive. Each file must implement the IPipelineFile interface.</param>
    /// <returns>A <see cref="ZipPackageResult"/> whose <see cref="ZipPackageResult.ZipPackage"/> is the generated ZIP file,
    /// or null if no valid input files were provided.</returns>
    /// <exception cref="ArgumentException">Thrown if no input files are provided.</exception>
    [PipelineProcessRun]
    public async Task<ZipPackageResult> RunAsync([UploadFiles] IPipelineFileList? uploadFiles, params IPipelineFile?[] input)
    {
        var allFiles = includeUploadFiles && uploadFiles != null
            ? uploadFiles.Files.Cast<IPipelineFile?>().Concat(input).ToArray()
            : input;

        if (allFiles.Length == 0)
        {
            var errorMessage = $"ZipPackageProcess: No input files provided.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        // Filter out null values, by casting to non-nullable IPipelineFile
        var validFiles = allFiles.OfType<IPipelineFile>().ToArray();
        IPipelineFile? zipTransferFile = null;

        LocalizedText statusMessage;
        if (validFiles.Length == 0)
        {
            statusMessage = NoFilesStatusMessage;
            logger.LogWarning($"ZipPackageProcess: No valid input files found. Returning null.");
        }
        else
        {
            zipTransferFile = pipelineFileManager.GeneratePipelineFile(archiveFileName, "zip");

            static string EntryName(IPipelineFile file) =>
                string.IsNullOrEmpty(file.OriginalRelativePath)
                    ? file.OriginalFileName
                    : $"{file.OriginalRelativePath.TrimEnd('/')}/{file.OriginalFileName}";

            var duplicateEntryNames = validFiles
                .GroupBy(EntryName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateEntryNames.Count > 0)
            {
                logger.LogWarning("ZipPackageProcess: Duplicate entry names detected in input: {DuplicateEntryNames}.", string.Join(", ", duplicateEntryNames));
            }

            using (var zipArchiveFileStream = zipTransferFile.OpenWriteFileStream())
            using (var zipArchive = new ZipArchive(zipArchiveFileStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in validFiles)
                {
                    var zipEntry = zipArchive.CreateEntry(EntryName(file));
                    using var zipEntryStream = zipEntry.Open();
                    using var fileStream = file.OpenReadFileStream();
                    fileStream.CopyTo(zipEntryStream);
                }
            }

            statusMessage = SuccessfulStatusMessageFormat
                .Map(msg => string.Format(CultureInfo.InvariantCulture, msg, validFiles.Length));
        }

        return new ZipPackageResult
        {
            StatusMessage = statusMessage,
            ZipPackage = zipTransferFile,
        };
    }
}
