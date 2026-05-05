using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process.IliToGeoPackage;

/// <summary>
/// Pipeline process that converts INTERLIS transfer files (XTF/ITF) to GeoPackage (.gpkg)
/// by handing each input off to a long-running <c>ili2gpkg-worker</c> service via a shared
/// filesystem. Geopilot never invokes <c>ili2gpkg</c> directly and never spawns containers,
/// so this process requires no container-runtime access from the geopilot host — the
/// previous pattern mounted the Docker socket, which is a root-equivalent escalation
/// surface, and has been removed.
/// </summary>
/// <remarks>
/// <para>Protocol: for each input file, a unique correlation id (Guid) is chosen. The
/// process writes the JSON sidecar and the XTF directly under their final names in
/// <see cref="inputRoot"/>, then writes a zero-byte <c>{correlationId}.input.ready</c>
/// sentinel last. The worker only consumes a job once it observes the sentinel, so a
/// half-written XTF or config can never be processed. The geopilot-to-worker contract
/// does not depend on rename atomicity, which is unreliable on bind-mounted filesystems
/// (Docker Desktop on Windows: virtiofs/9p/SMB).</para>
/// <para>Output uses the same shape: the worker writes either <c>{correlationId}.gpkg</c>
/// (success) or <c>{correlationId}.error</c> (failure), then a zero-byte
/// <c>{correlationId}.output.ready</c> sentinel last. This process polls only for the
/// output sentinel, then briefly waits for the corresponding payload to become visible
/// and stable in size before consuming. Output is copied into the pipeline file manager
/// and the shared output files are then deleted. If the worker is not up, outputs never
/// appear and the timeout kicks in with a clear diagnostic pointing at the mount
/// configuration.</para>
/// <para>The worker image lives under <c>docker/ili2gpkg-worker/</c> and must be deployed
/// alongside geopilot with access to the same input/output directories.</para>
/// </remarks>
internal class IliToGeoPackageProcess
{
    private readonly string inputRoot;
    private readonly string outputRoot;
    private readonly TimeSpan pollInterval;
    private readonly TimeSpan timeout;

    private readonly bool import;
    private readonly bool disableValidation;
    private readonly bool createBasketCol;
    private readonly bool smart2Inheritance;
    private readonly bool schemaImport;
    private readonly HashSet<string> models;
    private readonly int? defaultSrsCode;

    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="IliToGeoPackageProcess"/> class.
    /// </summary>
    /// <param name="inputRoot">Shared directory where the worker picks up XTF files. Must be writable by geopilot and readable by the worker — typically a bind-mount or Azure Files share mounted into both.</param>
    /// <param name="outputRoot">Shared directory where the worker writes produced GeoPackage files. Must be writable by the worker and readable by geopilot.</param>
    /// <param name="pollIntervalMs">How often to check the output directory for results while waiting on a conversion. Defaults to 500 ms.</param>
    /// <param name="timeoutSeconds">Maximum time to wait for the worker to produce output for a given file before giving up and failing the step. Defaults to 600 s (10 min).</param>
    /// <param name="import">If true, passes <c>--import</c> to ili2gpkg to import data from the XTF into the GeoPackage. Defaults to true.</param>
    /// <param name="disableValidation">If true, passes <c>--disableValidation</c> to skip validation during import. Defaults to false.</param>
    /// <param name="createBasketCol">If true, passes <c>--createBasketCol</c> to add a <c>T_basket</c> column on each table. Defaults to false.</param>
    /// <param name="smart2Inheritance">If true, passes <c>--smart2Inheritance</c> for the "smart2" INTERLIS inheritance mapping. Defaults to false.</param>
    /// <param name="schemaImport">If true, passes <c>--schemaimport</c> to create the GeoPackage schema from the INTERLIS model before importing data. Defaults to false.</param>
    /// <param name="models">Set of INTERLIS model names, passed via <c>--models</c> as a semicolon-separated list. Null or empty lets ili2gpkg infer the models from the XTF header.</param>
    /// <param name="defaultSrsCode">Optional EPSG code of the default coordinate reference system (e.g. <c>2056</c> for Swiss CH1903+/LV95).</param>
    /// <param name="pipelineFileManager">Used to register produced .gpkg files into the pipeline working directory.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public IliToGeoPackageProcess(
        string inputRoot,
        string outputRoot,
        int? pollIntervalMs,
        int? timeoutSeconds,
        bool? import,
        bool? disableValidation,
        bool? createBasketCol,
        bool? smart2Inheritance,
        bool? schemaImport,
        HashSet<string>? models,
        int? defaultSrsCode,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(inputRoot))
            throw new ArgumentException("inputRoot must be set for IliToGeoPackageProcess.", nameof(inputRoot));
        if (string.IsNullOrWhiteSpace(outputRoot))
            throw new ArgumentException("outputRoot must be set for IliToGeoPackageProcess.", nameof(outputRoot));

        this.inputRoot = Path.GetFullPath(inputRoot);
        this.outputRoot = Path.GetFullPath(outputRoot);
        this.pollInterval = pollIntervalMs.HasValue ? TimeSpan.FromMilliseconds(pollIntervalMs.Value) : TimeSpan.FromMilliseconds(500);
        this.timeout = timeoutSeconds.HasValue ? TimeSpan.FromSeconds(timeoutSeconds.Value) : TimeSpan.FromMinutes(10);

        this.import = import ?? true;
        this.disableValidation = disableValidation ?? false;
        this.createBasketCol = createBasketCol ?? false;
        this.smart2Inheritance = smart2Inheritance ?? false;
        this.schemaImport = schemaImport ?? false;
        this.models = models ?? new HashSet<string>();
        this.defaultSrsCode = defaultSrsCode;

        this.pipelineFileManager = pipelineFileManager ?? throw new ArgumentNullException(nameof(pipelineFileManager));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure the shared dirs exist so a first-run-on-a-clean-host doesn't fail before
        // the worker has had a chance to create them itself.
        Directory.CreateDirectory(this.inputRoot);
        Directory.CreateDirectory(this.outputRoot);

        // Log the resolved absolute paths once. When the configured paths are relative
        // (common in dev) the resolution depends on the process CWD, and a mismatch with
        // the worker's mount is the #1 reason "the worker doesn't grab my file" — this
        // log line makes the actual directory obvious from a single run.
        this.logger.LogInformation(
            "IliToGeoPackageProcess configured: inputRoot='{InputRoot}', outputRoot='{OutputRoot}', timeout={Timeout}, pollInterval={PollInterval}",
            this.inputRoot,
            this.outputRoot,
            this.timeout,
            this.pollInterval);
    }

    /// <summary>
    /// Runs the file-drop protocol once per input file and returns the produced GeoPackage
    /// files. Files are processed sequentially — parallelism is the worker's concern.
    /// </summary>
    /// <param name="iliFiles">INTERLIS transfer files piped in from an upstream step.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary with <c>gpkg_files</c> as an <see cref="IPipelineFile"/> array and a localized <c>status_message</c>.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile[] iliFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(iliFiles);

        var gpkgFiles = new List<IPipelineFile>();
        foreach (var inputFile in iliFiles)
        {
            var gpkgFile = await ConvertFileAsync(inputFile, cancellationToken).ConfigureAwait(false);
            gpkgFiles.Add(gpkgFile);
        }

        return new Dictionary<string, object?>
        {
            { "gpkg_files", gpkgFiles.ToArray() },
            { "status_message", BuildStatusMessage(gpkgFiles.Count) },
        };
    }

    /// <summary>
    /// Executes the three-phase protocol (drop input, poll for output, consume result) for
    /// a single file. Internal for testability — tests can drive the protocol directly
    /// against a temp directory and a mock worker.
    /// </summary>
    /// <param name="inputFile">The XTF/ITF file to convert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline file containing the produced GeoPackage.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the worker reports a failure via the <c>.error</c> sentinel.</exception>
    /// <exception cref="TimeoutException">Thrown when the worker does not produce any output within <see cref="timeout"/>. Typically indicates the worker container is not running or the shared directories are not mounted into it.</exception>
    internal async Task<IPipelineFile> ConvertFileAsync(IPipelineFile inputFile, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var xtfPath = Path.Combine(inputRoot, $"{correlationId}.xtf");
        var configPath = Path.Combine(inputRoot, $"{correlationId}.config.json");
        var inputReadyPath = Path.Combine(inputRoot, $"{correlationId}.input.ready");
        var okPath = Path.Combine(outputRoot, $"{correlationId}.gpkg");
        var errorPath = Path.Combine(outputRoot, $"{correlationId}.error");
        var outputReadyPath = Path.Combine(outputRoot, $"{correlationId}.output.ready");

        // Write payloads under their final names, then drop the sentinel. The worker only
        // triggers on the sentinel, so the order here defines the contract: any visible
        // sentinel implies both payloads have already been closed.
        await WriteConfigSidecarAsync(configPath, inputFile.OriginalFileName, cancellationToken).ConfigureAwait(false);
        await WriteInputXtfAsync(inputFile, xtfPath, cancellationToken).ConfigureAwait(false);
        await CreateSentinelAsync(inputReadyPath, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Dispatched '{File}' to ili2gpkg-worker as correlationId={CorrelationId}; waiting for output.",
            inputFile.OriginalFileName,
            correlationId);

        await WaitForOutputSentinelAsync(outputReadyPath, correlationId, inputFile.OriginalFileName, cancellationToken).ConfigureAwait(false);

        // The sentinel guarantees that exactly one of okPath / errorPath was written before
        // it appeared. On a flaky bind-mount the payload may still be lagging the sentinel;
        // wait briefly for whichever one materialises.
        var resolvedPath = await WaitForResolvedPayloadAsync(okPath, errorPath, correlationId, inputFile.OriginalFileName, cancellationToken).ConfigureAwait(false);

        if (resolvedPath == errorPath)
        {
            var errorMessage = await ReadErrorSentinelAsync(errorPath).ConfigureAwait(false);
            TryDeleteFile(errorPath);
            TryDeleteFile(outputReadyPath);
            throw new InvalidOperationException(
                $"ili2gpkg worker reported failure for '{inputFile.OriginalFileName}' (correlationId={correlationId}): {errorMessage}");
        }

        await WaitForSizeStableAsync(okPath, cancellationToken).ConfigureAwait(false);
        var gpkgFile = await CopyOutputToPipelineFileAsync(okPath, inputFile.OriginalFileNameWithoutExtension, cancellationToken).ConfigureAwait(false);
        TryDeleteFile(okPath);
        TryDeleteFile(outputReadyPath);

        logger.LogInformation("Consumed gpkg output for correlationId={CorrelationId} ('{File}').", correlationId, inputFile.OriginalFileName);
        return gpkgFile;
    }

    /// <summary>
    /// Writes the JSON config sidecar (<c>{correlationId}.config.json</c>) directly under its
    /// final name. Visibility of the file does not signal completion — the
    /// <c>{correlationId}.input.ready</c> sentinel does. Writing the sidecar before the XTF
    /// also keeps the worker's belt-and-braces config-read retry on the rare side.
    /// </summary>
    private async Task WriteConfigSidecarAsync(string configPath, string originalFileName, CancellationToken cancellationToken)
    {
        var config = new WorkerConfigSidecar
        {
            OriginalFileName = originalFileName,
            Import = import,
            DisableValidation = disableValidation,
            CreateBasketCol = createBasketCol,
            Smart2Inheritance = smart2Inheritance,
            SchemaImport = schemaImport,
            Models = models.ToArray(),
            DefaultSrsCode = defaultSrsCode,
        };

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, config, ConfigJsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies the upload stream into <see cref="inputRoot"/> under its final name. Like the
    /// config sidecar this no longer relies on rename atomicity; the
    /// <c>{correlationId}.input.ready</c> sentinel is the worker's signal that the XTF is
    /// fully written.
    /// </summary>
    private static async Task WriteInputXtfAsync(IPipelineFile inputFile, string xtfPath, CancellationToken cancellationToken)
    {
        await using var src = inputFile.OpenReadFileStream();
        await using var dst = File.Create(xtfPath);
        await src.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a zero-byte sentinel file. Sentinels are written as the final step of either
    /// side of the protocol; their existence implies the corresponding payloads were already
    /// closed before the sentinel was created.
    /// </summary>
    private static async Task CreateSentinelAsync(string sentinelPath, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(sentinelPath);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls for the <c>.output.ready</c> sentinel. Returns when it is observed; throws
    /// <see cref="TimeoutException"/> on <see cref="timeout"/> elapse.
    /// </summary>
    private async Task WaitForOutputSentinelAsync(string outputReadyPath, string correlationId, string originalFileName, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while (!File.Exists(outputReadyPath))
            {
                await Task.Delay(pollInterval, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"ili2gpkg-worker did not produce output for '{originalFileName}' (correlationId={correlationId}) within {timeout}. " +
                $"Verify the ili2gpkg-worker service is running and that both containers mount the same directories " +
                $"(geopilot wrote to '{inputRoot}', polled for output under '{outputRoot}').");
        }
    }

    /// <summary>
    /// After the output sentinel appears, briefly waits for either the success
    /// (<paramref name="okPath"/>) or error (<paramref name="errorPath"/>) payload to become
    /// visible. The sentinel guarantees the worker already wrote one of them, but on flaky
    /// bind-mounts the payload visibility may lag the sentinel by a short interval.
    /// </summary>
    /// <returns>The path that materialised — either <paramref name="okPath"/> or <paramref name="errorPath"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when neither payload appears after the resolution window.</exception>
    private async Task<string> WaitForResolvedPayloadAsync(string okPath, string errorPath, string correlationId, string originalFileName, CancellationToken cancellationToken)
    {
        // Five seconds is generous: the worker writes the payload before the sentinel and
        // most filesystems have caught up by the time the sentinel is even observed. We
        // poll instead of stat-once-and-fail so a flaky mount has a chance to settle.
        using var resolutionCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, resolutionCts.Token);

        try
        {
            while (true)
            {
                // If both happen to be present (e.g. a previous failed run left .error
                // behind), the .error is the more informative signal to surface.
                if (File.Exists(errorPath))
                    return errorPath;
                if (File.Exists(okPath))
                    return okPath;

                await Task.Delay(pollInterval, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"ili2gpkg-worker wrote the output sentinel for '{originalFileName}' (correlationId={correlationId}) " +
                $"but neither '{Path.GetFileName(okPath)}' nor '{Path.GetFileName(errorPath)}' became visible. " +
                $"This indicates a broken worker contract or a severely degraded shared filesystem.");
        }
    }

    /// <summary>
    /// Reads the worker's <c>.error</c> payload. Tolerates a transient read failure on flaky
    /// filesystems by surfacing a generic message — the caller still throws.
    /// </summary>
    private async Task<string> ReadErrorSentinelAsync(string errorPath)
    {
        try
        {
            var message = await File.ReadAllTextAsync(errorPath, CancellationToken.None).ConfigureAwait(false);
            return message.Trim();
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read error sentinel at '{Path}'; surfacing a generic failure.", errorPath);
            return "(error file present but not readable)";
        }
    }

    /// <summary>
    /// Defence-in-depth for bind-mounts where a file may be visible before its content has
    /// fully propagated: re-stats the gpkg with a short delay until the size stabilises.
    /// Bounded so a genuinely streaming-write bug can't hang the pipeline forever.
    /// </summary>
    private async Task WaitForSizeStableAsync(string path, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        var checkDelay = TimeSpan.FromMilliseconds(200);

        long previous = -1;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var current = new FileInfo(path).Length;
            if (current == previous && current > 0)
                return;

            previous = current;
            await Task.Delay(checkDelay, cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning(
            "Output file '{Path}' did not stabilise in size after {Attempts} checks ({Total}); proceeding anyway.",
            path,
            maxAttempts,
            checkDelay * maxAttempts);
    }

    /// <summary>
    /// Copies the worker-produced gpkg into a pipeline-managed file that downstream steps
    /// can consume. The worker's gpkg in the shared dir is deleted after this returns.
    /// </summary>
    private async Task<IPipelineFile> CopyOutputToPipelineFileAsync(string okPath, string originalBaseName, CancellationToken cancellationToken)
    {
        var pipelineFile = pipelineFileManager.GeneratePipelineFile(originalBaseName, "gpkg");
        await using (var src = File.OpenRead(okPath))
        await using (var dst = pipelineFile.OpenWriteFileStream())
        {
            await src.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
        }

        return pipelineFile;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-fatal: the orphan-sweep on the worker side will eventually reclaim it.
            logger.LogDebug(ex, "Could not delete shared file '{Path}'; orphan sweeper will handle it.", path);
        }
    }

    /// <summary>
    /// Builds the per-language status message shown in the delivery UI.
    /// </summary>
    private static Dictionary<string, string> BuildStatusMessage(int count)
    {
        var isPlural = count != 1;
        var nounDe = isPlural ? "GeoPackages" : "GeoPackage";
        var nounEn = isPlural ? "GeoPackages" : "GeoPackage";
        var nounFr = isPlural ? "GeoPackages créés" : "GeoPackage créé";
        var nounIt = isPlural ? "GeoPackages creati" : "GeoPackage creato";

        return new Dictionary<string, string>
        {
            { "de", $"{count} {nounDe} erstellt" },
            { "en", $"{count} {nounEn} created" },
            { "fr", $"{count} {nounFr}" },
            { "it", $"{count} {nounIt}" },
        };
    }

    /// <summary>
    /// JSON contract for the per-file config sidecar dropped alongside each XTF. The worker
    /// parses this to build its ili2gpkg command line. Keep field names stable — changes
    /// are a breaking change between geopilot and the deployed worker image.
    /// </summary>
    private sealed class WorkerConfigSidecar
    {
        public string OriginalFileName { get; init; } = string.Empty;

        public bool Import { get; init; }

        public bool DisableValidation { get; init; }

        public bool CreateBasketCol { get; init; }

        public bool Smart2Inheritance { get; init; }

        public bool SchemaImport { get; init; }

        public string[] Models { get; init; } = Array.Empty<string>();

        public int? DefaultSrsCode { get; init; }
    }
}
