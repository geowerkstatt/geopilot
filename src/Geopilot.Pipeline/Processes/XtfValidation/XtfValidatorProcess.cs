using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Processes.XtfValidation;

/// <summary>
/// Process for validating INTERLIS transfer files with ilivalidator through the ilitools-wrapper.
/// </summary>
internal class XtfValidatorProcess
{
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

    private readonly IlivalidatorArgs validatorArgs;
    private readonly IPipelineFile? modelRepository;
    private readonly IPipelineFile? refMappingFile;
    private readonly IIlivalidatorClient ilivalidatorClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    /// <summary>
    /// Create a new instance of the <see cref="XtfValidatorProcess"/> class.
    /// </summary>
    /// <param name="validationProfile">Optional validation profile, given as the dataset id that indexes it in one of the <paramref name="modelDirs"/>. An <c>ilidata:</c> prefix may be included.</param>
    /// <param name="refMapping">Optional mapping that names the reference data the tool loads for the extent of the validation, given as <c>ilidata:</c> and the dataset id that indexes it in one of the <paramref name="modelDirs"/>. Passed to <c>--refmapping</c> as written, without the prefix the profile gets. Which entries apply is selected by the <c>scope</c> input of the run.</param>
    /// <param name="modelDirs">Optional INTERLIS model repositories as a semicolon separated list, searched in the given order. Replaces the default of the tool entirely.</param>
    /// <param name="allObjectsAccessible">Whether a reference to an object outside the validated file is an error. Defaults to true.</param>
    /// <param name="pluginIds">Optional ilitools-wrapper plugins as a semicolon separated list of ids, which provide the user defined functions a model may call in its constraints. Which ids exist is a property of the wrapper deployment.</param>
    /// <param name="toolVersion">Optional version of ilivalidator to run, selected from the versions the wrapper deployment offers. Without it the deployment default runs.</param>
    /// <param name="modelRepository">Optional ZIP archive of a model repository, given as the path of a file the deployment ships. The wrapper unpacks it into its own subfolder, which <paramref name="modelDirs"/> reaches through <c>%ITF_DIR/repository</c>.</param>
    /// <param name="refMappingFile">Optional mapping that names the reference data, given as the path of a file the deployment ships and sent along with every validation in place of <paramref name="refMapping"/>; the wrapper rejects a validation with both. The reference data it names resolve through the <paramref name="modelDirs"/>.</param>
    /// <param name="ilivalidatorClient">Client of the ilitools-wrapper that runs the validation.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the validation process.</param>
    /// <param name="logger">Logger instance for logging messages during the validation process.</param>
    public XtfValidatorProcess(
        string? validationProfile,
        string? refMapping,
        string? modelDirs,
        bool? allObjectsAccessible,
        string? pluginIds,
        string? toolVersion,
        IPipelineFile? modelRepository,
        IPipelineFile? refMappingFile,
        IIlivalidatorClient ilivalidatorClient,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        this.modelRepository = modelRepository;
        this.refMappingFile = refMappingFile;
        this.validatorArgs = new IlivalidatorArgs
        {
            ModelDirs = IlitoolsHelper.SplitConfiguredList(modelDirs),
            MetaConfig = IlitoolsHelper.ToIlidataReference(validationProfile),
            RefMapping = string.IsNullOrWhiteSpace(refMapping) ? null : refMapping.Trim(),
            AllObjectsAccessible = allObjectsAccessible ?? true,
            PluginIds = IlitoolsHelper.SplitConfiguredList(pluginIds),
            ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? null : toolVersion.Trim(),
        };

        this.ilivalidatorClient = ilivalidatorClient;
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;
    }

    /// <summary>
    /// Runs the validation process for the specified transfer file.
    /// </summary>
    /// <param name="transferFile">The transfer file to validate. Cannot be null.</param>
    /// <param name="modelFiles">
    /// The delivered model files (<c>.ili</c>) to pass on to the validation, handed on verbatim. The pipeline decides
    /// what counts as a model, typically by wiring the output of a matcher that selects the <c>.ili</c> files of the
    /// upload. They only take part in the validation when <c>modelDirs</c> contains the entry <c>%ITF_DIR/models</c>,
    /// whose position ranks them against the other model sources. Unwired, no models are sent.
    /// </param>
    /// <param name="referenceDataFiles">
    /// The delivered reference data (XTF or XML, such as a supplier's catalog) the tool loads next to the transfer
    /// file, handed on verbatim. The pipeline decides what counts as reference data by what it wires, and with it
    /// whether delivered reference data count at all. Unwired, none are sent.
    /// </param>
    /// <param name="scope">
    /// The extent of this validation, for example the BFS number of the delivered municipality, passed to the tool as
    /// <c>--scope</c>. An input rather than configuration, because it belongs to the delivery: a literal in the step,
    /// or the <c>Scope</c> the XTF metadata extractor reads from the transfer file. Unwired or blank, no scope is passed.
    /// </param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="XtfValidatorResult"/> instance containing the results of the validation process.</returns>
    [PipelineProcessRun]
    public async Task<XtfValidatorResult> RunAsync(IPipelineFile transferFile, IPipelineFile[] modelFiles, IPipelineFile[] referenceDataFiles, string? scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transferFile);
        ArgumentNullException.ThrowIfNull(modelFiles);
        ArgumentNullException.ThrowIfNull(referenceDataFiles);

        var runArgs = validatorArgs with { Scope = string.IsNullOrWhiteSpace(scope) ? null : scope.Trim() };

        logger.LogInformation($"Validating transfer file <{transferFile.OriginalFileName}>...");

        var errorLog = pipelineFileManager.GeneratePipelineFile("errorLog", "log");
        var xtfLog = pipelineFileManager.GeneratePipelineFile("xtfLog", "xtf");

        if (modelFiles.Length > 0)
        {
            logger.LogInformation($"Forwarding {modelFiles.Length} delivered model file(s) to the validation.");
        }

        if (referenceDataFiles.Length > 0)
        {
            logger.LogInformation($"Forwarding {referenceDataFiles.Length} delivered reference data file(s) to the validation.");
        }

        var result = await ilivalidatorClient.ValidateAsync(runArgs, transferFile, errorLog, xtfLog, modelRepository, modelFiles, refMappingFile, referenceDataFiles, cancellationToken);

        logger.LogInformation($"Validation of transfer file <{transferFile.OriginalFileName}> finished. Successful: <{result.Success}>.");

        await IlitoolsHelper.AssertNoUndefinedValidationOutcome(Ilitool.Ilivalidator, errorLog, result.Success, validatorArgs.MetaConfig, validConfig: null, validatorArgs.ModelDirs, validatorArgs.PluginIds, cancellationToken);

        // ilivalidator writes both logs whenever it runs, and a tool that does not run surfaces as a failed call,
        // so both files are always handed on.
        return new XtfValidatorResult
        {
            ValidationSuccessful = result.Success,
            StatusMessage = result.Success ? SuccessStatusMessage : FailureStatusMessage,
            ErrorLog = errorLog,
            XtfLog = xtfLog,
        };
    }
}
