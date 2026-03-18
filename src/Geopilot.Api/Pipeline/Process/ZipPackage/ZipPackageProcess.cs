using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.IO.Compression;

namespace Geopilot.Api.Pipeline.Process.ZipPackage;

/// <summary>
/// Represents a pipeline process that handles packaging <see cref="IPipelineFile"/> to a ZIP file which is also provided in a <see cref="IPipelineFile"/>.
/// </summary>
/// <remarks>This class is intended for use within a data processing pipeline where ZIP package handling of <see cref="IPipelineFile"/> is required.
/// All non-null <see cref="IPipelineFile"/> provided in the input will be included in the created ZIP archive. The resulting ZIP file is then made available as an output of the process.
/// The ZIP archive is provided under the key 'zip_package' in the output. If no valid input files are provided, null is returned.</remarks>
internal class ZipPackageProcess
{
    private const string OutputMappingZipPackage = "zip_package";
    private const string DefaultArchiveFileName = "archive";

    private ILogger logger;

    private string archiveFileName;
    private IPipelineFileManager pipelineFileManager;
    private Guid jobId;

    /// <summary>
    /// Creates a new instance of the <see cref="ZipPackageProcess"/> class with the specified configuration settings.
    /// </summary>
    /// <param name="archiveFileName">The ZIP file name to use for the output archive without file extension. If null, the default name 'archive' will be used.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the ZIP packaging process.</param>
    /// <param name="logger">Logger instance for logging messages during the initialization process.</param>
    /// <param name="jobId">The unique identifier for the current job, used for logging and tracking purposes.</param>
    public ZipPackageProcess(string? archiveFileName, IPipelineFileManager pipelineFileManager, ILogger logger, Guid jobId)
    {
        this.logger = logger;
        if (!string.IsNullOrEmpty(archiveFileName))
            this.archiveFileName = archiveFileName;
        else
            this.archiveFileName = DefaultArchiveFileName;
        this.pipelineFileManager = pipelineFileManager;
        this.jobId = jobId;
    }

    /// <summary>
    /// Creates a ZIP archive containing the specified input files and returns a dictionary mapping the output key to
    /// the resulting ZIP file.
    /// </summary>
    /// <param name="input">An array of input files to include in the ZIP archive. Each file must implement the IPipelineTransferFile interface.</param>
    /// <returns>A dictionary containing a single entry that maps the output key to the generated ZIP file as a
    /// PipelineTransferFile instance, or null if no valid input files were provided.</returns>
    /// <exception cref="ArgumentException">Thrown if no input files are provided.</exception>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(params IPipelineFile?[] input)
    {
        if (input.Length == 0)
        {
            var errorMessage = $"ZipPackageProcess: No input files provided (job: {jobId}).";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        // Filter out null values, by casting to non-nullable IPipelineTransferFile
        var validFiles = input.OfType<IPipelineFile>().ToArray();
        IPipelineFile? zipTransferFile = null;

        if (validFiles.Length == 0)
        {
            logger.LogWarning($"ZipPackageProcess: No valid input files found (job: {jobId}). Returning null.");
        }
        else
        {
            zipTransferFile = pipelineFileManager.GeneratePipelineFile(archiveFileName, "zip");

            using (var zipArchiveFileStream = zipTransferFile.OpenWriteFileStream())
            using (var zipArchive = new ZipArchive(zipArchiveFileStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in validFiles)
                {
                    var zipEntry = zipArchive.CreateEntry(file.OriginalFileName);
                    using var zipEntryStream = zipEntry.Open();
                    using var fileStream = file.OpenReadFileStream();
                    fileStream.CopyTo(zipEntryStream);
                }
            }
        }

        return new Dictionary<string, object?>()
        {
            { OutputMappingZipPackage, zipTransferFile },
        };
    }
}
