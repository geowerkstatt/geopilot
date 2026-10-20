using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgDataImportProcess
{
    private readonly Ili2GpkgArgs ili2GpkgArgs;
    private readonly IPipelineFile? modelRepository;
    private readonly IIli2GpkgClient ili2GpkgClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    private static readonly LocalizedText SuccessStatusMessage = new Dictionary<string, string>
    {
        { "de", "Der Daten-Import war erfolgreich." },
        { "fr", "L'importation des données s'est déroulée avec succès." },
        { "it", "L'importazione dei dati è andata a buon fine." },
        { "en", "The data import was successful." },
    };

    private static readonly LocalizedText FailureStatusMessage = new Dictionary<string, string>
    {
        { "de", "Der Daten-Import hat Fehler ergeben. Details stehen in der Log-Datei." },
        { "fr", "L'importation des données a généré des erreurs. Vous trouverez plus de détails dans le fichier journal." },
        { "it", "L'importazione dei dati ha generato degli errori. I dettagli sono riportati nel file di log." },
        { "en", "The data import resulted in errors. Details can be found in the log file." },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Ili2GpkgDataImportProcess"/> class.
    /// </summary>
    public Ili2GpkgDataImportProcess(
        string? metaConfig,
        string? modelDirs,
        string? toolVersion,
        string? pluginIds,
        bool? doSchemaImport,
        bool? deleteData,
        bool? disableValidation,
        bool? skipPolygonBuilding,
        bool? skipGeometryErrors,
        bool? skipReferenceErrors,
        bool? strokeArcs,
        bool? importTid,
        bool? importBid,
        bool? createBasketCol,
        bool? createFk,
        bool? createGeomIdx,
        bool? createEnumTabs,
        bool? createTidCol,
        bool? beautifyEnumDispName,
        bool? smart1Inheritance,
        bool? smart2Inheritance,
        bool? sqlEnableNull,
        bool? sqlColsAsText,
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

            DoSchemaImport = doSchemaImport ?? false,
            DeleteData = deleteData ?? false,
            DisableValidation = disableValidation ?? false,
            SkipPolygonBuilding = skipPolygonBuilding ?? false,
            SkipGeometryErrors = skipGeometryErrors ?? false,
            SkipReferenceErrors = skipReferenceErrors ?? false,
            StrokeArcs = strokeArcs ?? false,
            ImportTid = importTid ?? false,
            ImportBid = importBid ?? false,

            // Schema import options
            CreateBasketCol = createBasketCol ?? false,
            CreateFk = createFk ?? false,
            CreateGeomIdx = createGeomIdx ?? false,
            CreateEnumTabs = createEnumTabs ?? false,
            CreateTidCol = createTidCol ?? false,
            BeautifyEnumDispName = beautifyEnumDispName ?? false,
            Smart1Inheritance = smart1Inheritance ?? false,
            Smart2Inheritance = smart2Inheritance ?? false,
            SqlEnableNull = sqlEnableNull ?? false,
            SqlColsAsText = sqlColsAsText ?? false,
        };
    }

    [PipelineProcessRun]
    public async Task<Ili2GpkgDatabaseResult> RunAsync(
        IPipelineFile[] transferFiles,
        IPipelineFile? dbFile,
        string? models,
        int? defaultSrsCode,
        string? dataset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transferFiles);
        if (transferFiles.Length == 0)
        {
            throw new ArgumentException("No transfer files provided for data import.", nameof(transferFiles));
        }

        var transferFormat = IlitoolsHelper.GetFormatOfTransferFiles(transferFiles);
        if (!(transferFormat is Ili2GpkgTransferFormat.Xtf or Ili2GpkgTransferFormat.Itf))
        {
            throw new ArgumentException("Format of the transfer files must be either 'xtf' or 'itf'.", nameof(transferFiles));
        }

        logger.LogInformation("Starting data import for {TransferCount} transfer file(s).", transferFiles.Length);

        var outputDbFile = pipelineFileManager.GeneratePipelineFile("data", "gpkg");

        var args = ili2GpkgArgs with
        {
            Models = IlitoolsHelper.SplitConfiguredList(models),
            DefaultSrsCode = defaultSrsCode,
            Dataset = dataset,
            TransferFormat = transferFormat,
        };

        var errorLogFile = pipelineFileManager.GeneratePipelineFile("import", "log");
        var xtfLogFile = pipelineFileManager.GeneratePipelineFile("import-log", "xtf");
        var result = await ili2GpkgClient.ImportAsync(
            args,
            inputFile: dbFile,
            outputFile: outputDbFile,
            transferFiles: transferFiles,
            logFile: errorLogFile,
            xtfLogFile: xtfLogFile,
            modelRepositoryArchive: modelRepository,
            cancellationToken: cancellationToken);

        await IlitoolsHelper.AssertNoUndefinedValidationOutcome(Ilitool.Ili2gpkg, errorLogFile, result.Success, args.MetaConfig, args.ValidConfig, args.ModelDirs, args.PluginIds, cancellationToken);

        return new Ili2GpkgDatabaseResult
        {
            Success = result.Success,
            StatusMessage = result.Success ? SuccessStatusMessage : FailureStatusMessage,
            DbFile = result.Success ? [outputDbFile] : [],
            ErrorLog = errorLogFile,
            XtfLog = xtfLogFile,
        };
    }
}
