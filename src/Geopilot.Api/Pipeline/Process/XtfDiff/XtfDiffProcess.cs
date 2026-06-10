using Geopilot.Api.Pipeline.Process.MapVisualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;
using System.Text.Json;

namespace Geopilot.Api.Pipeline.Process.XtfDiff;

/// <summary>
/// Compares two XTF files using the XTF-Diff-Tool
/// (https://github.com/geowerkstatt/XTF-Diff-Tool) and turns the geometry differences into a
/// map-visualization config (<see cref="MapVisualizationConfig"/>). The raw diff JSON produced by
/// the tool is exposed as <c>diff_file</c>, the map config as <c>map_visualization_config_file</c>.
/// The alphabetically first file (by original file name) is treated as the old state, the other
/// one as the new state.
/// </summary>
/// <remarks>
/// <para>
/// The tool runs in the separate, long-running xtf-diff-worker container (see
/// <c>docker/xtf-diff-worker</c>), driven through a shared jobs directory: this process drops a
/// job folder with the two XTF files and an <c>input.ready</c> sentinel, then polls until the
/// worker signals completion with an <c>output.ready</c> sentinel — analogous to how
/// <see cref="XtfValidation.XtfValidatorProcess"/> uploads to the Interlis check service and
/// polls its status endpoint.
/// </para>
/// <para>
/// Note that the XTF-Diff-Tool matches objects across the two files by their transfer id and
/// only compares objects whose class declares a stable OID in the INTERLIS model (for example
/// <c>OID AS INTERLIS.UUIDOID</c> on the topic). Objects of classes without a stable OID
/// declaration are skipped entirely, so data of such models always yields an empty diff.
/// Added or deleted whole objects are reported as a single "object" entry without geometry
/// values; geometry features on the map therefore only appear for changed geometries and for
/// optional geometry attributes added to or removed from objects present in both files.
/// </para>
/// </remarks>
internal class XtfDiffProcess
{
    private const string OutputMappingDiffFile = "diff_file";
    private const string OutputMappingConfigFile = "map_visualization_config_file";
    private const string OutputMappingStatusMessage = "status_message";

    private const string XtfFileExtension = "xtf";

    // Fixed file names of the xtf-diff-worker file-drop protocol (see docker/xtf-diff-worker/README.md).
    private const string OldXtfFileName = "old.xtf";
    private const string NewXtfFileName = "new.xtf";
    private const string ArgsFileName = "args.json";
    private const string DiffFileName = "diff.json";
    private const string InputReadyFileName = "input.ready";
    private const string OutputReadyFileName = "output.ready";
    private const string ErrorLogFileName = "error.log";

    private static readonly Dictionary<string, string> SuccessfulStatusMessageFormat = new()
    {
        { "de", "Vergleich von '{0}' (alt) mit '{1}' (neu): {2} Geometrie(n) hinzugefügt, {3} gelöscht, {4} geändert." },
        { "fr", "Comparaison de '{0}' (ancien) avec '{1}' (nouveau) : {2} géométrie(s) ajoutée(s), {3} supprimée(s), {4} modifiée(s)." },
        { "it", "Confronto di '{0}' (vecchio) con '{1}' (nuovo): {2} geometria(e) aggiunta(e), {3} eliminata(e), {4} modificata(e)." },
        { "en", "Compared '{0}' (old) with '{1}' (new): {2} geometry(ies) added, {3} deleted, {4} changed." },
    };

    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;
    private readonly string jobsDirectory;
    private readonly TimeSpan pollInterval;
    private readonly string? modelDirectory;
    private readonly string baseMapWmtsCapabilitiesUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtfDiffProcess"/> class.
    /// </summary>
    /// <param name="jobsDirectory">The jobs directory shared with the xtf-diff-worker container, absolute or relative to the working directory.</param>
    /// <param name="pollInterval">Optional polling interval in milliseconds for checking job completion. If not provided, a default of 2000ms will be used.</param>
    /// <param name="modelDirectory">Optional INTERLIS model search directories, passed to the tool as <c>--modeldir</c>.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">Optional override for the base map WMTS capabilities URL. Defaults to the swisstopo map of Switzerland.</param>
    /// <param name="pipelineFileManager">Manages the step's temporary output files.</param>
    /// <param name="logger">Logger instance for logging messages during the comparison.</param>
    public XtfDiffProcess(
        string jobsDirectory,
        int? pollInterval,
        string? modelDirectory,
        string? baseMapWmtsCapabilitiesUrl,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;
        this.jobsDirectory = jobsDirectory;
        this.pollInterval = pollInterval != null ? TimeSpan.FromMilliseconds((double)pollInterval) : TimeSpan.FromSeconds(2);
        this.modelDirectory = modelDirectory;
        this.baseMapWmtsCapabilitiesUrl = string.IsNullOrWhiteSpace(baseMapWmtsCapabilitiesUrl)
            ? MapVisualizationProcess.DefaultBaseMapWmtsCapabilitiesUrl
            : baseMapWmtsCapabilitiesUrl;
    }

    /// <summary>
    /// Compares the two XTF files among <paramref name="xtfFiles"/> with the XTF-Diff-Tool and
    /// builds the map-visualization config of the geometry differences.
    /// </summary>
    /// <param name="xtfFiles">The input files. Exactly two of them must have the <c>xtf</c> extension; other files are ignored.</param>
    /// <param name="cancellationToken">Cancellation token of the pipeline run; cancelling abandons the job (the worker's orphan sweep cleans it up).</param>
    /// <returns>The output map with the raw diff JSON, the generated map config and a status message.</returns>
    /// <exception cref="InvalidOperationException">If not exactly two XTF files are supplied or the XTF-Diff-Tool fails.</exception>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile[] xtfFiles, CancellationToken cancellationToken)
    {
        var orderedXtfFiles = xtfFiles
            .Where(file => string.Equals(file.FileExtension, XtfFileExtension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.OriginalFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedXtfFiles.Count != 2)
            throw new InvalidOperationException($"Expected exactly two XTF files to compare, but found <{orderedXtfFiles.Count}>.");

        var oldXtfFile = orderedXtfFiles[0];
        var newXtfFile = orderedXtfFiles[1];

        // Each request gets its own job folder inside the shared jobs directory; the worker only
        // consumes it once the input.ready sentinel (written last) appears.
        var jobsRoot = Path.GetFullPath(jobsDirectory);
        Directory.CreateDirectory(jobsRoot);
        var jobDirectory = Path.Combine(jobsRoot, Guid.NewGuid().ToString());
        Directory.CreateDirectory(jobDirectory);

        try
        {
            await CopyToJobFolderAsync(oldXtfFile, Path.Combine(jobDirectory, OldXtfFileName), cancellationToken);
            await CopyToJobFolderAsync(newXtfFile, Path.Combine(jobDirectory, NewXtfFileName), cancellationToken);
            if (!string.IsNullOrWhiteSpace(modelDirectory))
            {
                using var argsStream = File.Create(Path.Combine(jobDirectory, ArgsFileName));
                await JsonSerializer.SerializeAsync(argsStream, new { modeldir = modelDirectory }, cancellationToken: cancellationToken);
            }

            await File.WriteAllBytesAsync(Path.Combine(jobDirectory, InputReadyFileName), Array.Empty<byte>(), cancellationToken);

            logger.LogInformation($"Comparing <{oldXtfFile.OriginalFileName}> (old) with <{newXtfFile.OriginalFileName}> (new) using the xtf-diff-worker, job <{Path.GetFileName(jobDirectory)}>...");
            await PollOutputReadyAsync(jobDirectory, cancellationToken);

            var errorLogPath = Path.Combine(jobDirectory, ErrorLogFileName);
            if (File.Exists(errorLogPath))
            {
                var errorLog = await File.ReadAllTextAsync(errorLogPath, cancellationToken);
                logger.LogError($"XTF-Diff-Tool failed: {errorLog}");
                throw new InvalidOperationException($"The XTF-Diff-Tool failed: {errorLog.Trim()}");
            }

            var diffFilePath = Path.Combine(jobDirectory, DiffFileName);
            if (!File.Exists(diffFilePath))
                throw new InvalidOperationException("The xtf-diff-worker signalled completion but produced neither a diff result nor an error log.");

            List<XtfDiffEntry> diffEntries;
            using (var diffStream = File.OpenRead(diffFilePath))
            {
                diffEntries = await JsonSerializer.DeserializeAsync<List<XtfDiffEntry>>(diffStream, cancellationToken: cancellationToken) ?? new List<XtfDiffEntry>();
            }

            var diffFile = pipelineFileManager.GeneratePipelineFile("xtfDiff", "json");
            using (var diffSourceStream = File.OpenRead(diffFilePath))
            using (var diffTargetStream = diffFile.OpenWriteFileStream())
            {
                await diffSourceStream.CopyToAsync(diffTargetStream, cancellationToken);
            }

            var config = XtfDiffMapConfigBuilder.Build(diffEntries, baseMapWmtsCapabilitiesUrl);
            var configFile = pipelineFileManager.GeneratePipelineFile("mapVisualizationConfig", "json");
            using (var configStream = configFile.OpenWriteFileStream())
            {
                await JsonSerializer.SerializeAsync(configStream, config, cancellationToken: cancellationToken);
            }

            return new Dictionary<string, object?>
            {
                { OutputMappingDiffFile, diffFile },
                { OutputMappingConfigFile, configFile },
                { OutputMappingStatusMessage, CreateStatusMessage(oldXtfFile, newXtfFile, diffEntries) },
            };
        }
        finally
        {
            try
            {
                Directory.Delete(jobDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Failed to delete the xtf-diff-worker job folder <{jobDirectory}>. The worker's orphan sweep will clean it up.");
            }
        }
    }

    private static async Task CopyToJobFolderAsync(IPipelineFile file, string targetPath, CancellationToken cancellationToken)
    {
        using var sourceStream = file.OpenReadFileStream();
        using var targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
    }

    /// <summary>
    /// Polls the job folder until the worker drops the <c>output.ready</c> sentinel. The sentinel is
    /// written last by the worker, after the diff result and log files have been closed, so its
    /// appearance guarantees the job's output files are complete.
    /// </summary>
    private async Task PollOutputReadyAsync(string jobDirectory, CancellationToken cancellationToken)
    {
        var outputReadyPath = Path.Combine(jobDirectory, OutputReadyFileName);
        while (!File.Exists(outputReadyPath))
        {
            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private static Dictionary<string, string> CreateStatusMessage(IPipelineFile oldXtfFile, IPipelineFile newXtfFile, List<XtfDiffEntry> diffEntries)
    {
        var geometryChanges = diffEntries
            .Where(entry => string.Equals(entry.ValueType, XtfDiffEntry.ValueTypeGeometry, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var addedCount = geometryChanges.Count(entry => string.Equals(entry.ChangeType, XtfDiffEntry.ChangeTypeAdded, StringComparison.OrdinalIgnoreCase));
        var deletedCount = geometryChanges.Count(entry => string.Equals(entry.ChangeType, XtfDiffEntry.ChangeTypeDeleted, StringComparison.OrdinalIgnoreCase));
        var changedCount = geometryChanges.Count(entry => string.Equals(entry.ChangeType, XtfDiffEntry.ChangeTypeChanged, StringComparison.OrdinalIgnoreCase));

        return SuccessfulStatusMessageFormat.ToDictionary(
            message => message.Key,
            message => string.Format(CultureInfo.InvariantCulture, message.Value, oldXtfFile.OriginalFileName, newXtfFile.OriginalFileName, addedCount, deletedCount, changedCount));
    }
}
