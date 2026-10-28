using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgExportProcess
{
    private readonly Ili2GpkgArgs ili2GpkgArgs;
    private readonly IPipelineFile? modelRepository;
    private readonly IIli2GpkgClient ili2GpkgClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    private static readonly LocalizedText SuccessStatusMessage = new Dictionary<string, string>
    {
        { "de", "Der Daten-Export war erfolgreich." },
        { "fr", "L'exportation des données s'est déroulée avec succès." },
        { "it", "L'esportazione dei dati è andata a buon fine." },
        { "en", "The data export was successful." },
    };

    private static readonly LocalizedText FailureStatusMessage = new Dictionary<string, string>
    {
        { "de", "Der Daten-Export hat Fehler ergeben. Details stehen in der Log-Datei." },
        { "fr", "L'exportation des données a généré des erreurs. Vous trouverez plus de détails dans le fichier journal." },
        { "it", "L'esportazione dei dati ha generato degli errori. I dettagli sono riportati nel file di log." },
        { "en", "The data export resulted in errors. Details can be found in the log file." },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Ili2GpkgExportProcess"/> class.
    /// </summary>
    /// <param name="metaConfig">Optional meta configuration, given as the dataset id that indexes it in one of the <paramref name="modelDirs"/>. An <c>ilidata:</c> prefix may be included.</param>
    /// <param name="modelDirs">Optional INTERLIS model repositories as a semicolon separated list, searched in the given order. Replaces the default of the tool entirely.</param>
    /// <param name="toolVersion">Optional version of ili2gpkg to run, selected from the versions the wrapper deployment offers. Without it the deployment default runs.</param>
    /// <param name="pluginIds">Optional plugin IDs as a semicolon separated list, used to validate the data during the export operation.</param>
    /// <param name="disableValidation">Optional flag to disable validation during the export operation.</param>
    /// <param name="modelRepository">Optional model repository archive.</param>
    /// <param name="ili2GpkgClient">Ili2gpkg client that runs the export operation.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the process.</param>
    /// <param name="logger">Logger instance for logging messages during the process.</param>
    public Ili2GpkgExportProcess(
        string? metaConfig,
        string? modelDirs,
        string? toolVersion,
        string? pluginIds,
        bool? disableValidation,
        IPipelineFile? modelRepository,
        IIli2GpkgClient ili2GpkgClient,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        this.modelRepository = modelRepository;
        this.ili2GpkgClient = ili2GpkgClient;
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;

        ili2GpkgArgs = new Ili2GpkgArgs
        {
            MetaConfig = IlitoolsHelper.ToIlidataReference(metaConfig),
            ModelDirs = IlitoolsHelper.SplitConfiguredList(modelDirs),
            ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? null : toolVersion.Trim(),
            PluginIds = IlitoolsHelper.SplitConfiguredList(pluginIds),
            DisableValidation = disableValidation ?? false,
        };
    }

    [PipelineProcessRun]
    public async Task<Ili2GpkgExportResult> RunAsync(
        IPipelineFile dbFile,
        string[]? models,
        string[]? topics,
        string[]? baskets,
        string[]? dataset,
        string[]? exportModels,
        Ili2GpkgTransferFormat? transferFormat,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFile);

        logger.LogInformation("Starting data export.");

        var args = ili2GpkgArgs with
        {
            Models = IlitoolsHelper.SplitConfiguredListEntries(models),
            Topics = IlitoolsHelper.SplitConfiguredListEntries(topics),
            Baskets = IlitoolsHelper.SplitConfiguredListEntries(baskets),
            Dataset = dataset == null ? null : string.Join(';', dataset),
            ExportModels = IlitoolsHelper.SplitConfiguredListEntries(exportModels),
            TransferFormat = transferFormat,
        };

        var fileExtension = transferFormat switch
        {
            Ili2GpkgTransferFormat.Xtf or null => "xtf",
            Ili2GpkgTransferFormat.Itf => "itf",
            Ili2GpkgTransferFormat.Gml or Ili2GpkgTransferFormat.Gml20 => "gml",
            _ => throw new ArgumentOutOfRangeException(nameof(transferFormat), transferFormat, "Unsupported transfer format."),
        };
        var transferFile = pipelineFileManager.GeneratePipelineFile("data", fileExtension);
        var errorLogFile = pipelineFileManager.GeneratePipelineFile("export", "log");
        var xtfLogFile = pipelineFileManager.GeneratePipelineFile("export-log", "xtf");
        var result = await ili2GpkgClient.ExportAsync(
            args,
            gpkgFile: dbFile,
            transferFile: transferFile,
            logFile: errorLogFile,
            xtfLogFile: xtfLogFile,
            modelRepositoryArchive: modelRepository,
            cancellationToken: cancellationToken);

        await IlitoolsHelper.AssertNoUndefinedValidationOutcome(Ilitool.Ili2gpkg, errorLogFile, result.Success, args.MetaConfig, args.ValidConfig, args.ModelDirs, args.PluginIds, cancellationToken);

        return new Ili2GpkgExportResult
        {
            Success = result.Success,
            StatusMessage = result.Success ? SuccessStatusMessage : FailureStatusMessage,
            TransferFile = result.Success ? [transferFile] : [],
            ErrorLog = errorLogFile,
            XtfLog = xtfLogFile,
        };
    }
}
