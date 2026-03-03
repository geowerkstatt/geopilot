using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.IO.Compression;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Represents a pipeline process that handles packaging <see cref="IPipelineTransferFile"/> to a ZIP file which is also provided in a <see cref="IPipelineTransferFile"/>.
/// </summary>
/// <remarks>This class is intended for use within a data processing pipeline where ZIP package handling of <see cref="IPipelineTransferFile"/> is required.
/// All non-null <see cref="IPipelineTransferFile"/> provided in the input will be included in the created ZIP archive. The resulting ZIP file is then made available as an output of the process.
/// The ZIP archive is provided under the key 'zip_package' in the output. If no valid input files are provided, null is returned.</remarks>
internal class ZipPackageProcess
{
    private const string OutputMappingZipPackage = "zip_package";
    private const string ConfiguratiionKeyArchiveFileName = "archive_file_name";

    private ILogger<ZipPackageProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ZipPackageProcess>();

    private Dictionary<string, string> config = new Dictionary<string, string>();

    private string ArchiveFileName
    {
        get
        {
            if (this.config != null && this.config.TryGetValue(ConfiguratiionKeyArchiveFileName, out var profile))
                return profile;
            else
                return "archive";
        }
    }

    /// <summary>
    /// Initializes the pipeline process with the specified configuration settings.
    /// </summary>
    /// <param name="config">The parameterization configuration for the process. provides the archive file name under the key 'archive_file_name'.</param>
    [PipelineProcessInitialize]
    public void Initialize(Dictionary<string, string> config)
    {
        this.config = config;
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
    public async Task<Dictionary<string, object?>> RunAsync(params IPipelineTransferFile?[] input)
    {
        if (input.Length == 0)
        {
            var errorMessage = "ZipPackageProcess: No input files provided.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        // Filter out null values, by casting to non-nullable IPipelineTransferFile
        var validFiles = input.OfType<IPipelineTransferFile>().ToArray();
        PipelineTransferFile? zipTransferFile = null;

        if (validFiles.Length == 0)
        {
            logger.LogWarning("ZipPackageProcess: No valid input files found. Returning null.");
        }
        else
        {
            zipTransferFile = new PipelineTransferFile(ArchiveFileName, Path.GetTempFileName().Replace(".tmp", ".zip"));
            using (var zipArchiveFileStream = new FileStream(zipTransferFile.FilePath, FileMode.Create))
            using (var zipArchive = new ZipArchive(zipArchiveFileStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in validFiles)
                {
                    var zipEntry = zipArchive.CreateEntry(file.OriginalFileName);
                    using var zipEntryStream = zipEntry.Open();
                    using var fileStream = file.OpenFileStream();
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
