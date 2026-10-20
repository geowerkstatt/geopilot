using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgUpdateProcess : Ili2GpkgProcessBase
{
    private readonly Ili2GpkgArgs ili2GpkgArgs;
    private readonly IPipelineFile? modelRepository;
    private readonly IIli2GpkgClient ili2GpkgClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    private static readonly LocalizedText SuccessStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die Daten-Aktualisierung war erfolgreich." },
        { "fr", "La mise à jour des données s'est déroulée avec succès." },
        { "it", "L'aggiornamento dei dati è andato a buon fine." },
        { "en", "The data update was successful." },
    };

    private static readonly LocalizedText FailureStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die Daten-Aktualisierung hat Fehler ergeben. Details stehen in der Log-Datei." },
        { "fr", "La mise à jour des données a généré des erreurs. Vous trouverez plus de détails dans le fichier journal." },
        { "it", "L'aggiornamento dei dati ha generato degli errori. I dettagli sono riportati nel file di log." },
        { "en", "The data update resulted in errors. Details can be found in the log file." },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Ili2GpkgUpdateProcess"/> class.
    /// </summary>
    public Ili2GpkgUpdateProcess(
        string? metaConfig,
        string? modelDirs,
        string? toolVersion,
        string? pluginIds,
        bool? disableValidation,
        bool? skipGeometryErrors,
        bool? skipReferenceErrors,
        bool? strokeArcs,
        IPipelineFile? modelRepository,
        IIli2GpkgClient ili2GpkgClient,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
        : base(
            metaConfig,
            modelDirs,
            toolVersion,
            pluginIds)
    {
        this.modelRepository = modelRepository;
        this.ili2GpkgClient = ili2GpkgClient;
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;

        ili2GpkgArgs = CreateCommonArgs() with
        {
            DisableValidation = disableValidation ?? false,
            SkipGeometryErrors = skipGeometryErrors ?? false,
            SkipReferenceErrors = skipReferenceErrors ?? false,
            StrokeArcs = strokeArcs ?? false,
        };
    }

    [PipelineProcessRun]
    public async Task<Ili2GpkgDatabaseResult> RunAsync(
        IPipelineFile[] transferFiles,
        IPipelineFile dbFile,
        string? models,
        string? dataset,
        int? defaultSrsCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transferFiles);
        ArgumentNullException.ThrowIfNull(dbFile);
        if (transferFiles.Length == 0)
        {
            throw new ArgumentException("No transfer files provided for data update.", nameof(transferFiles));
        }

        var transferFormat = GetFormatOfTransferFiles(transferFiles);
        if (!(transferFormat is Ili2GpkgTransferFormat.Xtf or Ili2GpkgTransferFormat.Itf))
        {
            throw new ArgumentException("Format of the transfer files must be either 'xtf' or 'itf'.", nameof(transferFiles));
        }

        logger.LogInformation("Starting data update for {TransferCount} transfer file(s).", transferFiles.Length);

        var outputDbFile = pipelineFileManager.GeneratePipelineFile("data", "gpkg");

        var args = ili2GpkgArgs with
        {
            Models = IlitoolsHelper.SplitConfiguredList(models),
            Dataset = dataset,
            DefaultSrsCode = defaultSrsCode,
            TransferFormat = transferFormat,
        };

        var errorLogFile = pipelineFileManager.GeneratePipelineFile("update", "log");
        var xtfLogFile = pipelineFileManager.GeneratePipelineFile("update-log", "xtf");
        var result = await ili2GpkgClient.UpdateAsync(
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
