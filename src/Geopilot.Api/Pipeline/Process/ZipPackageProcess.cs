using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Test.Pipeline;
using System.IO.Compression;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Represents a pipeline process that handles packaging <see cref="IPilelineTransferFile"/> to a ZIP file which is also provided in a <see cref="IPilelineTransferFile"/>.
/// </summary>
/// <remarks>This class is intended for use within a data processing pipeline where ZIP package handling of <see cref="IPilelineTransferFile"/> is required.
/// All <see cref="IPilelineTransferFile"/> provided in the input <see cref="ProcessData"/> will be included in the created ZIP archive. The resulting ZIP file is then made available as an output of the process.
/// It implements the <see cref="IPipelineProcess"/> interface. This type is internal and not intended for direct use outside of the pipeline infrastructure.
/// The ZIP archive is provided under the key 'zip_package' in the <see cref="ProcessData"/> output.</remarks>
internal class ZipPackageProcess : IPipelineProcess
{
    private const string OutputMappingZipPackage = "zip_package";
    private const string ConfiguratiionKeyArchiveFileName = "archive_file_name";

    private ILogger<ZipPackageProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ZipPackageProcess>();

    private DataHandlingConfig? dataHandlingConfig;

    private Parameterization? Config { get; set; }

    private string ArchiveFileName
    {
        get
        {
            if (this.Config != null && this.Config.TryGetValue(ConfiguratiionKeyArchiveFileName, out var profile))
                return profile;
            else
                return "archive";
        }
    }

    /// <summary>
    /// Initializes the pipeline process with the specified configuration settings.
    /// </summary>
    /// <param name="dataHandlingConfig">The data handling configuration to be used for the pipeline process. Cannot be null.</param>
    /// <param name="config">The parameterization configuration for the process. provides the archive file name under the key 'archive_file_name'.</param>
    [PipelineProcessInitialize]
    public void Initialize(DataHandlingConfig dataHandlingConfig, Parameterization config)
    {
        this.dataHandlingConfig = dataHandlingConfig;
        this.Config = config;
    }

    /// <inheritdoc/>
    public async Task<ProcessData> Run(ProcessData inputData)
    {
        var outputData = new ProcessData();

        var inputFiles = inputData.Data
            .Values
            .Select(d => d.Data)
            .Where(d => d is IPilelineTransferFile)
            .Cast<IPilelineTransferFile>()
            .ToList();

        if (inputFiles.Count == 0)
        {
            var errorMessage = "ZipPackageProcess: No valid input files found.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        using var zipArchiveMemoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(zipArchiveMemoryStream, ZipArchiveMode.Create, true))
        {
            inputFiles
                .ForEach(file =>
                {
                    var zipEntry = zipArchive.CreateEntry(file.OrginalFileName);
                    using var zipEntryStream = zipEntry.Open();
                    using var fileStream = file.OpenFileStream();
                    fileStream.CopyTo(zipEntryStream);
                });
        }

        var zipTransferFile = new PilelineTransferFile(ArchiveFileName, Path.GetTempFileName().Replace(".tmp", ".zip"));

        using var zipArchiveFileStream = new FileStream(zipTransferFile.FilePath, FileMode.Create);
        zipArchiveMemoryStream.Seek(0, SeekOrigin.Begin);
        zipArchiveMemoryStream.CopyTo(zipArchiveFileStream);

        if (dataHandlingConfig != null)
        {
            outputData.AddData(dataHandlingConfig.GetOutputMapping(OutputMappingZipPackage), new ProcessDataPart(zipTransferFile));
        }
        else
        {
            var errorMessage = $"ZipPackageProcess: DataHandlingConfig is null. Cannot add output data for mapping '{OutputMappingZipPackage}'.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        return outputData;
    }
}
