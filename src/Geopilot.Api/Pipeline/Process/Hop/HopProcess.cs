using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;

namespace Geopilot.Api.Pipeline.Process.Hop;

/// <summary>
/// Pipeline process that runs a Hop pipeline (or workflow) on the supplied input files via the
/// file-drop <see cref="HopClient"/>. The produced files are exposed under <c>output_files</c> and a
/// localized status message under <c>status_message</c>. The process itself is protocol-agnostic; the
/// file-drop protocol lives entirely in <see cref="HopClient"/>.
/// </summary>
internal sealed class HopProcess
{
    private const string OutputMappingOutputFiles = "output_files";
    private const string OutputMappingStatusMessage = "status_message";

    private static readonly Dictionary<string, string> SuccessStatusMessageFormat = new()
    {
        { "de", "Hop-Pipeline erfolgreich ausgeführt, {0} Datei(en) erzeugt." },
        { "fr", "Pipeline Hop exécuté avec succès, {0} fichier(s) généré(s)." },
        { "it", "Pipeline Hop eseguito correttamente, {0} file generati." },
        { "en", "Hop pipeline completed successfully, {0} file(s) produced." },
    };

    private static readonly Dictionary<string, string> FailureStatusMessage = new()
    {
        { "de", "Hop-Pipeline fehlgeschlagen." },
        { "fr", "Le pipeline Hop a échoué." },
        { "it", "Pipeline Hop non riuscito." },
        { "en", "Hop pipeline failed." },
    };

    private readonly HopClient hopClient;
    private readonly string pipeline;
    private readonly Dictionary<string, string> hopParameters;
    private readonly ILogger logger;

    /// <summary>
    /// Creates a new instance of the <see cref="HopProcess"/> class.
    /// </summary>
    /// <param name="jobsDirectory">Base directory for the job folders, shared with the Hop worker (from appsettings).</param>
    /// <param name="pipeline">Name of the Hop pipeline file to run (from the pipeline definition).</param>
    /// <param name="timeoutSeconds">Optional timeout for the Hop run in seconds. Defaults to 600.</param>
    /// <param name="pollInterval">Optional poll interval for the output sentinel in milliseconds. Defaults to 1000.</param>
    /// <param name="hopParameters">Optional Apache HOP parameters.</param>
    /// <param name="pipelineFileManager">File manager used to allocate the output files.</param>
    /// <param name="logger">Logger.</param>
    public HopProcess(
        string jobsDirectory,
        string pipeline,
        int? timeoutSeconds,
        int? pollInterval,
        Dictionary<string, string>? hopParameters,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(jobsDirectory))
        {
            throw new ArgumentException("Jobs directory must be set.", nameof(jobsDirectory));
        }

        if (string.IsNullOrWhiteSpace(pipeline))
        {
            throw new ArgumentException("Hop pipeline name must be set.", nameof(pipeline));
        }

        var options = new HopClientOptions
        {
            JobsDirectory = jobsDirectory,
        };
        if (timeoutSeconds is not null)
        {
            options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
        }

        if (pollInterval is not null)
        {
            options.PollInterval = TimeSpan.FromMilliseconds(pollInterval.Value);
        }

        this.pipeline = pipeline;
        this.hopParameters = hopParameters ?? new Dictionary<string, string>(StringComparer.Ordinal);
        this.logger = logger;
        this.hopClient = new HopClient(options, pipelineFileManager, logger);
    }

    /// <summary>
    /// Runs the configured Hop pipeline on <paramref name="inputFiles"/>.
    /// </summary>
    /// <param name="inputFiles">The files to feed into the Hop pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary with the produced files under <c>output_files</c> and a localized status message under <c>status_message</c>.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile[] inputFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputFiles);

        var args = new HopArgs(pipeline, hopParameters);
        var result = await hopClient.RunAsync(inputFiles, args, cancellationToken);

        Dictionary<string, string> statusMessage;
        if (result.Success)
        {
            statusMessage = SuccessStatusMessageFormat
                .ToDictionary(msg => msg.Key, msg => string.Format(CultureInfo.InvariantCulture, msg.Value, result.OutputFiles.Count));
            logger.LogInformation("HopProcess: pipeline '{Pipeline}' produced {Count} output file(s).", pipeline, result.OutputFiles.Count);
        }
        else
        {
            statusMessage = FailureStatusMessage;
            logger.LogWarning("HopProcess: pipeline '{Pipeline}' failed. Worker log: {Log}", pipeline, result.Log);
        }

        return new Dictionary<string, object?>
        {
            { OutputMappingOutputFiles, result.OutputFiles.ToArray() },
            { OutputMappingStatusMessage, statusMessage },
        };
    }
}
