using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Test.Pipeline;
using System.IO.Compression;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Represents a pipeline process that handles packaging <see cref="IPipelineTransferFile"/> to a ZIP file which is also provided in a <see cref="IPipelineTransferFile"/>.
/// </summary>
/// <remarks>This class is intended for use within a data processing pipeline where ZIP package handling of <see cref="IPipelineTransferFile"/> is required.
/// All <see cref="IPipelineTransferFile"/> provided in the input will be included in the created ZIP archive. The resulting ZIP file is then made available as an output of the process.
/// The ZIP archive is provided under the key 'zip_package' in the output.</remarks>
internal class ZipPackageProcess
{
    private const string OutputMappingZipPackage = "zip_package";
    private const string ConfiguratiionKeyArchiveFileName = "archive_file_name";

    private ILogger<ZipPackageProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ZipPackageProcess>();

    private Parameterization config = new Parameterization();

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
    public void Initialize(Parameterization config)
    {
        this.config = config;
    }

    /// <summary>
    /// Creates a ZIP archive from the provided input files and returns the resulting process data.
    /// </summary>
    /// <remarks>Only objects in the input list that implement IPipelineTransferFile are included in the ZIP
    /// archive. The method requires a valid data handling configuration to map the output ZIP package.</remarks>
    /// <param name="input">A list of objects representing input files to be packaged. Each object must implement the IPipelineTransferFile
    /// interface to be included in the ZIP archive.</param>
    /// <returns>A ProcessData instance containing the ZIP archive created from the input files.</returns>
    /// <exception cref="ArgumentException">Thrown if no valid input files are found in the input list, or if the data handling configuration is not set.</exception>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object>> RunAsync(params IPipelineTransferFile[] input)
    {
        if (input.Length == 0)
        {
            var errorMessage = "ZipPackageProcess: No valid input files found.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        var zipTransferFile = new PipelineTransferFile(ArchiveFileName, Path.GetTempFileName().Replace(".tmp", ".zip"));
        using var zipArchiveFileStream = new FileStream(zipTransferFile.FilePath, FileMode.Create);
        using (var zipArchive = new ZipArchive(zipArchiveFileStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in input)
            {
                var zipEntry = zipArchive.CreateEntry(file.OrginalFileName);
                using var zipEntryStream = zipEntry.Open();
                using var fileStream = file.OpenFileStream();
                fileStream.CopyTo(zipEntryStream);
            }
        }

        return new Dictionary<string, object>()
        {
            { OutputMappingZipPackage, zipTransferFile },
        };
    }
}
