using Geopilot.Api.Pipeline.Process.MapVisualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Geopilot.Api.Pipeline.Process.XtfDiff;

/// <summary>
/// Compares two XTF files using the external XTF-Diff-Tool
/// (https://github.com/geowerkstatt/XTF-Diff-Tool) and turns the geometry differences into a
/// map-visualization config (<see cref="MapVisualizationConfig"/>). The raw diff JSON produced by
/// the tool is exposed as <c>diff_file</c>, the map config as <c>map_visualization_config_file</c>.
/// The alphabetically first file (by original file name) is treated as the old state, the other
/// one as the new state.
/// </summary>
internal class XtfDiffProcess
{
    private const string OutputMappingDiffFile = "diff_file";
    private const string OutputMappingConfigFile = "map_visualization_config_file";
    private const string OutputMappingStatusMessage = "status_message";

    private const string XtfFileExtension = "xtf";
    private const string DefaultJavaPath = "java";

    private static readonly Dictionary<string, string> SuccessfulStatusMessageFormat = new()
    {
        { "de", "Vergleich von '{0}' (alt) mit '{1}' (neu): {2} Geometrie(n) hinzugefügt, {3} gelöscht, {4} geändert." },
        { "fr", "Comparaison de '{0}' (ancien) avec '{1}' (nouveau) : {2} géométrie(s) ajoutée(s), {3} supprimée(s), {4} modifiée(s)." },
        { "it", "Confronto di '{0}' (vecchio) con '{1}' (nuovo): {2} geometria(e) aggiunta(e), {3} eliminata(e), {4} modificata(e)." },
        { "en", "Compared '{0}' (old) with '{1}' (new): {2} geometry(ies) added, {3} deleted, {4} changed." },
    };

    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;
    private readonly string javaPath;
    private readonly string? xtfDiffToolJarPath;
    private readonly string? modelDirectory;
    private readonly string baseMapWmtsCapabilitiesUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtfDiffProcess"/> class.
    /// </summary>
    /// <param name="javaPath">Optional path to the Java executable used to run the XTF-Diff-Tool. Defaults to <c>java</c> on the PATH. The tool requires Java 25 (LTS) or newer.</param>
    /// <param name="xtfDiffToolJarPath">Path to the XTF-Diff-Tool jar, absolute or relative to the application base directory. Required at run time.</param>
    /// <param name="modelDirectory">Optional INTERLIS model search directories, passed to the tool as <c>--modeldir</c>.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">Optional override for the base map WMTS capabilities URL. Defaults to the swisstopo map of Switzerland.</param>
    /// <param name="pipelineFileManager">Manages the step's temporary output files.</param>
    /// <param name="logger">Logger instance for logging messages during the comparison.</param>
    public XtfDiffProcess(
        string? javaPath,
        string? xtfDiffToolJarPath,
        string? modelDirectory,
        string? baseMapWmtsCapabilitiesUrl,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;
        this.javaPath = string.IsNullOrWhiteSpace(javaPath) ? DefaultJavaPath : javaPath;
        this.xtfDiffToolJarPath = xtfDiffToolJarPath;
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
    /// <param name="cancellationToken">Cancellation token of the pipeline run; cancelling kills the external tool.</param>
    /// <returns>The output map with the raw diff JSON, the generated map config and a status message.</returns>
    /// <exception cref="InvalidOperationException">If not exactly two XTF files are supplied, the tool is not configured or the tool fails.</exception>
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
        var jarPath = ResolveJarPath();

        // The external tool works on file system paths while pipeline files only expose streams,
        // so both inputs are copied into a dedicated working directory (separate subdirectories,
        // as both files may carry the same original name).
        var workingDirectory = Directory.CreateTempSubdirectory("xtf-diff-").FullName;
        try
        {
            var oldFilePath = await CopyToDirectoryAsync(oldXtfFile, Path.Combine(workingDirectory, "old"), cancellationToken);
            var newFilePath = await CopyToDirectoryAsync(newXtfFile, Path.Combine(workingDirectory, "new"), cancellationToken);
            var diffFilePath = Path.Combine(workingDirectory, "diff.json");

            logger.LogInformation($"Comparing <{oldXtfFile.OriginalFileName}> (old) with <{newXtfFile.OriginalFileName}> (new) using the XTF-Diff-Tool...");
            await RunDiffToolAsync(jarPath, oldFilePath, newFilePath, diffFilePath, cancellationToken);

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
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Failed to delete the XTF-Diff-Tool working directory <{workingDirectory}>.");
            }
        }
    }

    /// <summary>
    /// Resolves the configured jar path against the application base directory and verifies the jar exists.
    /// </summary>
    private string ResolveJarPath()
    {
        if (string.IsNullOrWhiteSpace(xtfDiffToolJarPath))
            throw new InvalidOperationException($"The XTF-Diff-Tool jar path is not configured. Configure <xtfDiffToolJarPath> for the <{nameof(XtfDiffProcess)}>.");

        var jarPath = Path.IsPathRooted(xtfDiffToolJarPath)
            ? xtfDiffToolJarPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, xtfDiffToolJarPath));

        if (!File.Exists(jarPath))
            throw new InvalidOperationException($"The XTF-Diff-Tool jar was not found at <{jarPath}>.");

        return jarPath;
    }

    private static async Task<string> CopyToDirectoryAsync(IPipelineFile file, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, file.OriginalFileName);
        using (var sourceStream = file.OpenReadFileStream())
        using (var targetStream = File.Create(filePath))
        {
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
        }

        return filePath;
    }

    /// <summary>
    /// Runs <c>java -jar &lt;tool&gt; [--modeldir &lt;dir&gt;] &lt;old&gt; &lt;new&gt; &lt;diff&gt;</c>
    /// and throws if the tool cannot be started or exits with a non-zero exit code.
    /// </summary>
    private async Task RunDiffToolAsync(string jarPath, string oldFilePath, string newFilePath, string diffFilePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(jarPath);
        if (!string.IsNullOrWhiteSpace(modelDirectory))
        {
            startInfo.ArgumentList.Add("--modeldir");
            startInfo.ArgumentList.Add(modelDirectory);
        }

        startInfo.ArgumentList.Add(oldFilePath);
        startInfo.ArgumentList.Add(newFilePath);
        startInfo.ArgumentList.Add(diffFilePath);

        using var toolProcess = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start the XTF-Diff-Tool with <{javaPath}>.");

        var standardOutputTask = toolProcess.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = toolProcess.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await toolProcess.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                toolProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The tool already exited; nothing to kill.
            }

            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        if (!string.IsNullOrWhiteSpace(standardOutput))
            logger.LogInformation($"XTF-Diff-Tool output: {standardOutput}");
        if (!string.IsNullOrWhiteSpace(standardError))
            logger.LogWarning($"XTF-Diff-Tool error output: {standardError}");

        if (toolProcess.ExitCode != 0)
            throw new InvalidOperationException($"The XTF-Diff-Tool exited with code <{toolProcess.ExitCode}>.");
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
