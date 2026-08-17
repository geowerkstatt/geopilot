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
    private readonly IIlivalidatorClient ilivalidatorClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    /// <summary>
    /// Create a new instance of the <see cref="XtfValidatorProcess"/> class.
    /// </summary>
    /// <param name="validationProfile">Optional validation profile, given as the dataset id that indexes it in one of the <paramref name="modelDirs"/>. An <c>ilidata:</c> prefix may be included.</param>
    /// <param name="modelDirs">Optional INTERLIS model repositories, searched in the given order. Replaces the default of the tool entirely.</param>
    /// <param name="allObjectsAccessible">Whether a reference to an object outside the validated file is an error. Defaults to true.</param>
    /// <param name="ilivalidatorClient">Client of the ilitools-wrapper that runs the validation.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the validation process.</param>
    /// <param name="logger">Logger instance for logging messages during the validation process.</param>
    public XtfValidatorProcess(string? validationProfile, IReadOnlyList<string>? modelDirs, bool? allObjectsAccessible, IIlivalidatorClient ilivalidatorClient, IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        this.validatorArgs = new IlivalidatorArgs
        {
            ModelDirs = modelDirs,
            MetaConfig = BuildMetaConfig(validationProfile),

            // The interlis-check-service replaced an empty profile with its bundled DEFAULT profile and that profile
            // set this option, so every validation ran with it. Keeping it on by default is what preserves the
            // previous behaviour; a pipeline that delivers parts of a dataset can turn it off.
            AllObjectsAccessible = allObjectsAccessible ?? true,
        };

        this.ilivalidatorClient = ilivalidatorClient;
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;
    }

    /// <summary>
    /// Runs the validation process for the specified transfer file.
    /// </summary>
    /// <param name="iliFile">The transfer file to validate. Cannot be null.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="XtfValidatorResult"/> instance containing the results of the validation process.</returns>
    [PipelineProcessRun]
    public async Task<XtfValidatorResult> RunAsync(IPipelineFile iliFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(iliFile);

        logger.LogInformation($"Validating transfer file <{iliFile.OriginalFileName}>...");

        var errorLog = pipelineFileManager.GeneratePipelineFile("errorLog", "log");
        var xtfLog = pipelineFileManager.GeneratePipelineFile("xtfLog", "xtf");

        var result = await ilivalidatorClient.ValidateAsync(validatorArgs, iliFile, errorLog, xtfLog, cancellationToken);

        logger.LogInformation($"Validation of transfer file <{iliFile.OriginalFileName}> finished. Successful: <{result.Success}>.");

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
