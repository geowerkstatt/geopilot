using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Geopilot.Pipeline.Processes.XtfDiff;

/// <summary>
/// Process that lists the changes between an old and a new state of an INTERLIS transfer file with the XTF-Diff-Tool
/// through the ilitools-wrapper.
/// </summary>
internal class XtfDiffProcess
{
    /// <summary>
    /// What the tool writes into its log when the two states use different INTERLIS versions, the first of the two checks
    /// it runs before comparing. Pinned by <c>XtfDiffClientIntegrationTest</c> against the installed tool.
    /// </summary>
    internal const string DifferentInterlisVersionsMarker = "XTF files use different INTERLIS versions";

    /// <summary>
    /// What the tool writes into its log when the two states use different models. Pinned by
    /// <c>XtfDiffClientIntegrationTest</c> against the installed tool.
    /// </summary>
    internal const string DifferentModelsMarker = "XTF files use different INTERLIS models";

    private static readonly LocalizedText NoChangesStatusMessage = new Dictionary<string, string>
    {
        { "de", "Keine Änderungen gefunden." },
        { "fr", "Aucune modification trouvée." },
        { "it", "Nessuna modifica trovata." },
        { "en", "No changes found." },
    };

    private static readonly LocalizedText OneChangeStatusMessage = new Dictionary<string, string>
    {
        { "de", "Eine Änderung gefunden." },
        { "fr", "Une modification trouvée." },
        { "it", "Una modifica trovata." },
        { "en", "One change found." },
    };

    private static readonly LocalizedText ChangesStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "{0} Änderungen gefunden." },
        { "fr", "{0} modifications trouvées." },
        { "it", "{0} modifiche trovate." },
        { "en", "{0} changes found." },
    };

    private static readonly LocalizedText DifferentInterlisVersionsStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die beiden Dateien lassen sich nicht vergleichen, weil sie verschiedene INTERLIS-Versionen verwenden." },
        { "fr", "Les deux fichiers ne peuvent pas être comparés, car ils utilisent des versions INTERLIS différentes." },
        { "it", "I due file non possono essere confrontati perché utilizzano versioni INTERLIS diverse." },
        { "en", "The two files cannot be compared because they use different INTERLIS versions." },
    };

    private static readonly LocalizedText DifferentModelsStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die beiden Dateien lassen sich nicht vergleichen, weil sie verschiedene Modelle verwenden." },
        { "fr", "Les deux fichiers ne peuvent pas être comparés, car ils utilisent des modèles différents." },
        { "it", "I due file non possono essere confrontati perché utilizzano modelli diversi." },
        { "en", "The two files cannot be compared because they use different models." },
    };

    private readonly IReadOnlyList<string>? modelDirs;
    private readonly IXtfDiffClient xtfDiffClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    /// <summary>
    /// Create a new instance of the <see cref="XtfDiffProcess"/> class.
    /// </summary>
    /// <param name="modelDirs">Optional INTERLIS model repositories as a semicolon separated list, searched in the given order: <c>http(s)</c> URLs and repositories the ilitools-wrapper offers as <c>%REPOSITORIES/&lt;id&gt;</c>. Without it the default of the tool applies.</param>
    /// <param name="xtfDiffClient">Client of the ilitools-wrapper that runs the XTF-Diff-Tool.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for the diff and its log.</param>
    /// <param name="logger">Logger instance for logging messages during the comparison.</param>
    public XtfDiffProcess(string? modelDirs, IXtfDiffClient xtfDiffClient, IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        this.modelDirs = SplitConfiguredList(modelDirs);
        this.xtfDiffClient = xtfDiffClient;
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;
    }

    /// <summary>
    /// Lists the changes from <paramref name="oldXtf"/> to <paramref name="newXtf"/>. Each state is a parameter of its own,
    /// so the process works with any source of the two (ADR 0019): two uploads assigned to their roles, or a previous
    /// delivery and an upload.
    /// </summary>
    /// <param name="oldXtf">The earlier state of the transfer file.</param>
    /// <param name="newXtf">The later state of the transfer file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="XtfDiffResult"/> with the changes, or the reason why the two states cannot be compared.</returns>
    [PipelineProcessRun]
    public async Task<XtfDiffResult> RunAsync(IPipelineFile oldXtf, IPipelineFile newXtf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(oldXtf);
        ArgumentNullException.ThrowIfNull(newXtf);

        logger.LogInformation($"Comparing <{oldXtf.OriginalFileName}> as the old state with <{newXtf.OriginalFileName}> as the new state...");

        var diff = pipelineFileManager.GeneratePipelineFile("diff", "json");
        var log = pipelineFileManager.GeneratePipelineFile("diffLog", "log");

        var compared = await xtfDiffClient.DiffAsync(modelDirs, oldXtf, newXtf, log, diff, cancellationToken);
        if (compared)
        {
            var changeCount = await CountChangesAsync(diff, cancellationToken);
            logger.LogInformation($"Comparison finished with {changeCount} change(s).");
            return new XtfDiffResult
            {
                ComparisonState = ComparisonState.Compared,
                StatusMessage = ChangesStatusMessage(changeCount),
                Diff = diff,
                Log = log,
            };
        }

        // States the tool refuses to compare are a result the definition decides on, any other failure says nothing
        // about the two states and fails the step.
        var notComparableStatusMessage = await FindNotComparableStatusMessageAsync(log, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The XTF-Diff-Tool could not compare <{oldXtf.OriginalFileName}> with <{newXtf.OriginalFileName}>. The diff log names the cause.");

        logger.LogInformation("The two states cannot be compared.");
        return new XtfDiffResult
        {
            ComparisonState = ComparisonState.NotComparable,
            StatusMessage = notComparableStatusMessage,
            Log = log,
        };
    }

    // shortcut: parses the whole diff to count its entries, a streaming count if huge diffs strain the memory (#1054 measures them)
    private static async Task<int> CountChangesAsync(IPipelineFile diff, CancellationToken cancellationToken)
    {
        await using var stream = await diff.OpenReadAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetArrayLength();
    }

    private static LocalizedText ChangesStatusMessage(int changeCount)
    {
        return changeCount switch
        {
            0 => NoChangesStatusMessage,
            1 => OneChangeStatusMessage,
            _ => ChangesStatusMessageFormat.Map(message => string.Format(CultureInfo.InvariantCulture, message, changeCount)),
        };
    }

    private static async Task<LocalizedText?> FindNotComparableStatusMessageAsync(IPipelineFile log, CancellationToken cancellationToken)
    {
        var path = await log.GetLocalPathAsync(cancellationToken);
        if (!File.Exists(path))
            return null;

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        if (content.Contains(DifferentInterlisVersionsMarker, StringComparison.Ordinal))
            return DifferentInterlisVersionsStatusMessage;
        if (content.Contains(DifferentModelsMarker, StringComparison.Ordinal))
            return DifferentModelsStatusMessage;

        return null;
    }

    /// <summary>
    /// Splits the configured repositories on the semicolon, the separator the tool itself uses for <c>--modeldir</c>.
    /// A single scalar value is what makes the parameter usable in the appsettings base configuration.
    /// </summary>
    private static string[]? SplitConfiguredList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var entries = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return entries.Length > 0 ? entries : null;
    }
}
