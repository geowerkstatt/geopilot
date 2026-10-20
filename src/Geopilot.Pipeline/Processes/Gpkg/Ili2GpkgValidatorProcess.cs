using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgValidatorProcess
{
    private readonly Ili2GpkgArgs ili2GpkgArgs;
    private readonly IPipelineFile? modelRepository;
    private readonly IIli2GpkgClient ili2GpkgClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    private static readonly LocalizedText SuccessStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die Validierung war erfolgreich." },
        { "fr", "La validation a réussi." },
        { "it", "La validazione è riuscita." },
        { "en", "Validation successful." },
    };

    private static readonly LocalizedText FailureStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die Validierung hat Fehler ergeben. Details stehen im Validierungsprotokoll." },
        { "fr", "La validation a détecté des erreurs. Les détails figurent dans le journal de validation." },
        { "it", "La validazione ha rilevato errori. I dettagli sono riportati nel protocollo di validazione." },
        { "en", "Validation found errors. See the validation log for details." },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Ili2GpkgValidatorProcess"/> class.
    /// </summary>
    public Ili2GpkgValidatorProcess(
        string? modelDirs,
        string? toolVersion,
        string? pluginIds,
        string? validConfig,
        bool? disableAreaValidation,
        bool? forceTypeValidation,
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
            ModelDirs = IlitoolsHelper.SplitConfiguredList(modelDirs),
            ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? null : toolVersion.Trim(),
            PluginIds = IlitoolsHelper.SplitConfiguredList(pluginIds),
            ValidConfig = IlitoolsHelper.ToIlidataReference(validConfig),

            DisableAreaValidation = disableAreaValidation ?? false,
            ForceTypeValidation = forceTypeValidation ?? false,
        };
    }

    [PipelineProcessRun]
    public async Task<Ili2GpkgValidatorResult> RunAsync(
        IPipelineFile dbFile,
        string? models,
        string? topics,
        string? baskets,
        string? dataset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFile);

        logger.LogInformation("Starting GPKG data validation.");

        var args = ili2GpkgArgs with
        {
            Models = IlitoolsHelper.SplitConfiguredList(models),
            Topics = IlitoolsHelper.SplitConfiguredList(topics),
            Baskets = IlitoolsHelper.SplitConfiguredList(baskets),
            Dataset = dataset,
        };

        var errorLogFile = pipelineFileManager.GeneratePipelineFile("validate", "log");
        var xtfLogFile = pipelineFileManager.GeneratePipelineFile("validate-log", "xtf");
        var result = await ili2GpkgClient.ValidateAsync(
            args,
            gpkgFile: dbFile,
            logFile: errorLogFile,
            xtfLogFile: xtfLogFile,
            modelRepositoryArchive: modelRepository,
            cancellationToken: cancellationToken);

        await IlitoolsHelper.AssertNoUndefinedValidationOutcome(Ilitool.Ili2gpkg, errorLogFile, result.Success, args.MetaConfig, args.ValidConfig, args.ModelDirs, args.PluginIds, cancellationToken);

        return new Ili2GpkgValidatorResult
        {
            ValidationSuccessful = result.Success,
            StatusMessage = result.Success ? SuccessStatusMessage : FailureStatusMessage,
            ErrorLog = errorLogFile,
            XtfLog = xtfLogFile,
        };
    }
}
