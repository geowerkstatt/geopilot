using Geopilot.PipelineCore.Pipeline;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process.Hop;

/// <summary>
/// File-drop client for the Hop worker service. Writes the input files, <c>args.json</c> and the
/// <c>input.ready</c> sentinel into a per-job folder, polls for the worker's <c>output.ready</c>
/// sentinel and collects the produced output files. Mirrors the protocol of the ili2gpkg worker;
/// see the worker README for the shared-folder contract.
/// </summary>
internal sealed class HopClient
{
    private const string ArgsFileName = "args.json";
    private const string InputDirectoryName = "input";
    private const string OutputDirectoryName = "output";
    private const string InputReadyFileName = "input.ready";
    private const string OutputReadyFileName = "output.ready";
    private const string SuccessLogFileName = "success.log";
    private const string ErrorLogFileName = "error.log";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false,
    };

    private readonly HopClientOptions options;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new <see cref="HopClient"/>.
    /// </summary>
    /// <param name="options">Client options; <see cref="HopClientOptions.JobsDirectory"/> must be set.</param>
    /// <param name="pipelineFileManager">File manager used to materialize the worker's output files.</param>
    /// <param name="logger">Logger.</param>
    public HopClient(HopClientOptions options, IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.pipelineFileManager = pipelineFileManager ?? throw new ArgumentNullException(nameof(pipelineFileManager));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(options.JobsDirectory))
        {
            throw new ArgumentException($"{nameof(HopClientOptions.JobsDirectory)} must be set.", nameof(options));
        }
    }

    /// <summary>
    /// Submits a single job to the Hop worker and waits for its completion.
    /// </summary>
    /// <param name="inputFiles">Files written into the job's <c>input/</c> directory, preserving their relative hierarchy.</param>
    /// <param name="args">Pipeline reference and parameters serialized to <c>args.json</c>.</param>
    /// <param name="cancellationToken">Token to cancel the operation. The job also times out after <see cref="HopClientOptions.Timeout"/>.</param>
    /// <returns>A <see cref="HopRunResult"/> with the produced output files and the worker log.</returns>
    public async Task<HopRunResult> RunAsync(IReadOnlyList<IPipelineFile> inputFiles, HopArgs args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputFiles);
        ArgumentNullException.ThrowIfNull(args);

        Directory.CreateDirectory(options.JobsDirectory);

        var jobId = Guid.NewGuid().ToString("N");
        var jobDir = Path.Combine(options.JobsDirectory, jobId);
        var inputDir = Path.Combine(jobDir, InputDirectoryName);
        var outputDir = Path.Combine(jobDir, OutputDirectoryName);
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        var argsFile = Path.Combine(jobDir, ArgsFileName);
        var inputReady = Path.Combine(jobDir, InputReadyFileName);
        var outputReady = Path.Combine(jobDir, OutputReadyFileName);
        var successLog = Path.Combine(jobDir, SuccessLogFileName);
        var errorLog = Path.Combine(jobDir, ErrorLogFileName);

        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        try
        {
            logger.LogDebug("Submitting Hop job {JobId} in {JobDir}.", jobId, jobDir);

            foreach (var file in inputFiles)
            {
                await WriteInputFileAsync(file, inputDir, token);
            }

            var payload = new ArgsPayload
            {
                Pipeline = args.Pipeline,
                Parameters = args.Parameters.Count > 0
                    ? new Dictionary<string, string>(args.Parameters, StringComparer.Ordinal)
                    : null,
            };

            await using (var argsStream = new FileStream(argsFile, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(argsStream, payload, JsonOptions, token);
                await argsStream.FlushAsync(token);
            }

            // input.ready must be written last so the worker never observes a half-written job.
            await using (new FileStream(inputReady, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
            {
            }

            while (!File.Exists(outputReady))
            {
                await Task.Delay(options.PollInterval, token);
            }

            if (File.Exists(successLog))
            {
                var outputs = await CollectOutputFilesAsync(outputDir, token);
                var log = await File.ReadAllTextAsync(successLog, token);
                logger.LogInformation("Hop job {JobId} succeeded with {Count} output file(s).", jobId, outputs.Count);
                return new HopRunResult(true, outputs, log);
            }

            if (File.Exists(errorLog))
            {
                var log = await File.ReadAllTextAsync(errorLog, token);
                logger.LogWarning("Hop job {JobId} failed.", jobId);
                return new HopRunResult(false, Array.Empty<IPipelineFile>(), log);
            }

            logger.LogWarning("Hop job {JobId} reported completion but produced neither success.log nor error.log.", jobId);
            return new HopRunResult(
                false,
                Array.Empty<IPipelineFile>(),
                $"Hop worker reported completion for job {jobId} but produced neither success.log nor error.log.");
        }
        finally
        {
            try
            {
                Directory.Delete(jobDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Failed to clean up Hop job folder {JobDir}; orphan sweep will reclaim it.", jobDir);
            }
        }
    }

    private static async Task WriteInputFileAsync(IPipelineFile file, string inputDir, CancellationToken cancellationToken)
    {
        var relativeDir = file.OriginalRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var targetDir = string.IsNullOrEmpty(relativeDir) ? inputDir : Path.Combine(inputDir, relativeDir);
        Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, file.OriginalFileName);

        await using var source = file.OpenReadFileStream();
        await using var destination = new FileStream(targetPath, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<IPipelineFile>> CollectOutputFilesAsync(string outputDir, CancellationToken cancellationToken)
    {
        var outputs = new List<IPipelineFile>();

        foreach (var path in Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(outputDir, path);
            var relativeDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path).TrimStart('.');

            var outputFile = pipelineFileManager.GeneratePipelineFile(relativeDir, fileNameWithoutExtension, extension);

            await using (var source = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            await using (var destination = outputFile.OpenWriteFileStream())
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            outputs.Add(outputFile);
        }

        return outputs;
    }

    private sealed class ArgsPayload
    {
        public string Pipeline { get; set; } = string.Empty;

        public Dictionary<string, string>? Parameters { get; set; }
    }
}
