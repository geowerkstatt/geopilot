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
    private const string MetaConfigPrefix = "ilidata:";

    /// <summary>
    /// What the tool writes into its log when it cannot resolve the configured meta configuration. It then exits with
    /// the same code as a failed validation, so recognizing this line is what keeps a broken configuration from being
    /// reported as invalid data. Pinned by <c>IlivalidatorClientIntegrationTest</c> against the installed tool.
    /// </summary>
    internal const string MetaConfigNotFoundMarker = "failed to get local copy of meta-config file";

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
    private readonly IIlivalidatorClient ilivalidatorClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    /// <summary>
    /// Create a new instance of the <see cref="XtfValidatorProcess"/> class.
    /// </summary>
    /// <param name="validationProfile">Optional validation profile, given as the dataset id that indexes it in one of the <paramref name="modelDirs"/>. An <c>ilidata:</c> prefix may be included.</param>
    /// <param name="modelDirs">Optional INTERLIS model repositories as a semicolon separated list, searched in the given order. Replaces the default of the tool entirely.</param>
    /// <param name="allObjectsAccessible">Whether a reference to an object outside the validated file is an error. Defaults to true.</param>
    /// <param name="pluginIds">Optional ilitools-wrapper plugins as a semicolon separated list of ids, which provide the user defined functions a model may call in its constraints. Which ids exist is a property of the wrapper deployment.</param>
    /// <param name="modelRepository">Optional ZIP archive of a model repository, given as the path of a file the deployment ships. The wrapper unpacks it into its own subfolder, which <paramref name="modelDirs"/> reaches through <c>%ITF_DIR/repository</c>.</param>
    /// <param name="ilivalidatorClient">Client of the ilitools-wrapper that runs the validation.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the validation process.</param>
    /// <param name="logger">Logger instance for logging messages during the validation process.</param>
    public XtfValidatorProcess(string? validationProfile, string? modelDirs, bool? allObjectsAccessible, string? pluginIds, IPipelineFile? modelRepository, IIlivalidatorClient ilivalidatorClient, IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        this.modelRepository = modelRepository;
        this.validatorArgs = new IlivalidatorArgs
        {
            ModelDirs = SplitConfiguredList(modelDirs),
            MetaConfig = BuildMetaConfig(validationProfile),
            AllObjectsAccessible = allObjectsAccessible ?? true,
            PluginIds = SplitConfiguredList(pluginIds),
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
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="XtfValidatorResult"/> instance containing the results of the validation process.</returns>
    [PipelineProcessRun]
    public async Task<XtfValidatorResult> RunAsync(IPipelineFile transferFile, IPipelineFile[] modelFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transferFile);
        ArgumentNullException.ThrowIfNull(modelFiles);

        logger.LogInformation($"Validating transfer file <{transferFile.OriginalFileName}>...");

        var errorLog = pipelineFileManager.GeneratePipelineFile("errorLog", "log");
        var xtfLog = pipelineFileManager.GeneratePipelineFile("xtfLog", "xtf");

        if (modelFiles.Length > 0)
        {
            logger.LogInformation($"Forwarding {modelFiles.Length} delivered model file(s) to the validation.");
        }

        var result = await ilivalidatorClient.ValidateAsync(validatorArgs, transferFile, errorLog, xtfLog, modelRepository, modelFiles, cancellationToken);

        logger.LogInformation($"Validation of transfer file <{transferFile.OriginalFileName}> finished. Successful: <{result.Success}>.");

        if (!result.Success && validatorArgs.MetaConfig != null && await LogReportsUnresolvedMetaConfigAsync(errorLog, cancellationToken))
        {
            var repositories = validatorArgs.ModelDirs is { Count: > 0 }
                ? string.Join(';', validatorArgs.ModelDirs)
                : "the default repositories of the tool";
            throw new InvalidOperationException(
                $"The validation profile <{validatorArgs.MetaConfig}> could not be resolved from <{repositories}>, so the transfer file was not validated. Check the validationProfile and modelDirs configuration of this process.");
        }

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

    private static async Task<bool> LogReportsUnresolvedMetaConfigAsync(IPipelineFile logFile, CancellationToken cancellationToken)
    {
        var path = await logFile.GetLocalPathAsync(cancellationToken);
        if (!File.Exists(path))
            return false;

        using var reader = new StreamReader(path);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Contains(MetaConfigNotFoundMarker, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Splits a configured list on the semicolon. A single scalar value is what makes such a parameter usable in the
    /// appsettings base configuration, where a pipeline definition cannot override it and only scalars are carried.
    /// For the repositories the separator is the one the tool itself uses for <c>--modeldir</c> and the wrapper
    /// rejects an entry containing it; a plugin id cannot contain it either, because it has to match a folder name
    /// the wrapper offers. So nothing is lost by splitting here.
    /// </summary>
    private static string[]? SplitConfiguredList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var entries = value
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return entries.Length > 0 ? entries : null;
    }

    private static string? BuildMetaConfig(string? validationProfile)
    {
        if (string.IsNullOrWhiteSpace(validationProfile))
            return null;

        // The tool resolves the profile through the repository index, so the configured value is a dataset id.
        return validationProfile.StartsWith(MetaConfigPrefix, StringComparison.OrdinalIgnoreCase)
            ? validationProfile
            : MetaConfigPrefix + validationProfile;
    }
}
