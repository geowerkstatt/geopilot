using Geopilot.Api.Validation;
using Geopilot.Api.Validation.Interlis;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process.XtfValidation;

/// <summary>
/// Process for validating ILI files.
/// </summary>
internal class XtfValidatorProcess : IDisposable
{
    private enum LogType
    {
        XtfLog,
        ErrorLog,
    }

    private const string OutputMappingErrorLog = "error_log";
    private const string OutputMappingXtfLog = "xtf_log";
    private const string OutputMappingValidationSuccessful = "validation_successful";
    private const string OutputMappingStatusMessage = "status_message";
    private const string UploadUrl = "/api/v1/upload";

    private static readonly JsonSerializerOptions JsonOptions;

    private ILogger logger;

    private HttpClient httpClient = new();

    private string validationProfile;
    private TimeSpan pollInterval;
    private IPipelineFileManager pipelineFileManager;
    private Guid jobId;

    static XtfValidatorProcess()
    {
        JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    /// <summary>
    /// Create a new instance of the <see cref="XtfValidatorProcess"/> class.
    /// </summary>
    /// <param name="checkServiceBaseUrl">Base URL for the Interlis check service.</param>
    /// <param name="validationProfile">Optional validation profile to use for the validation process.</param>
    /// <param name="pollInterval">Optional polling interval in milliseconds for checking the validation status. If not provided, a default of 2000ms will be used.</param>
    /// <param name="pipelineFileManager">The pipeline file manager for managing temporary files during the validation process.</param>
    /// <param name="logger">Logger instance for logging messages during the validation process.</param>
    /// <param name="jobId">The unique identifier for the current job.</param>
    public XtfValidatorProcess(string checkServiceBaseUrl, string? validationProfile, int? pollInterval, IPipelineFileManager pipelineFileManager, ILogger logger, Guid jobId)
    {
        this.httpClient.BaseAddress = new Uri(checkServiceBaseUrl);
        this.httpClient.DefaultRequestHeaders.Accept.Clear();
        this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(validationProfile))
            this.validationProfile = validationProfile;
        else
            this.validationProfile = string.Empty;

        if (pollInterval != null)
            this.pollInterval = TimeSpan.FromMilliseconds((double)pollInterval);
        else
            this.pollInterval = TimeSpan.FromSeconds(2);

        this.logger = logger;
        this.pipelineFileManager = pipelineFileManager;
        this.jobId = jobId;
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="XtfValidatorProcess"/>.
    /// </summary>
    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    /// <summary>
    /// Runs the validation process for the specified ILI file.
    /// </summary>
    /// <param name="iliFile">The ILI file to validate. Cannot be null.</param>
    /// <param name="cancellationToken">Cancellation token to canclel the operation.</param>
    /// <returns>A ProcessData instance containing the results of the validation process.</returns>
    /// <exception cref="ArgumentException">Thrown if the input ILI file is invalid.</exception>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile iliFile, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Validating transfer file <{iliFile.OriginalFileName}> (job: {jobId})...");
        var uploadResponse = await UploadTransferFileAsync(iliFile, iliFile.OriginalFileName, this.validationProfile, cancellationToken);
        var statusResponse = await PollStatusAsync(uploadResponse.StatusUrl!, cancellationToken);
        var logFiles = await DownloadLogFilesAsync(statusResponse, cancellationToken);

        var outputs = new Dictionary<string, object?>
        {
            { OutputMappingValidationSuccessful, statusResponse.Status == InterlisStatusResponseStatus.Completed },
            { OutputMappingStatusMessage, statusResponse.StatusMessage ?? string.Empty },
            { OutputMappingErrorLog, logFiles.GetValueOrDefault(LogType.ErrorLog) },
            { OutputMappingXtfLog, logFiles.GetValueOrDefault(LogType.XtfLog) },
        };

        return outputs;
    }

    private async Task<InterlisUploadResponse> UploadTransferFileAsync(IPipelineFile file, string transferFile, string? interlisValidationProfile, CancellationToken cancellationToken)
    {
        using var fileStream = file.OpenReadFileStream() ?? throw new ArgumentException("Invalid input ILI file stream.");
        using var streamContent = new StreamContent(fileStream);
        using var profileStringContent = new StringContent(interlisValidationProfile ?? string.Empty);
        using var formData = new MultipartFormDataContent
        {
            { streamContent, "file", transferFile },
            { profileStringContent, "profile" },
        };

        using var response = await this.httpClient.PostAsync(UploadUrl, formData, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions, cancellationToken);
            logger.LogError($"Upload of transfer file <{transferFile}> to interlis-check-service failed (job: {jobId}).");
            throw new ValidationFailedException(problemDetails?.Detail ?? "Invalid transfer file");
        }

        logger.LogInformation($"Uploaded transfer file <{transferFile}> to interlis-check-service. Status code <{response.StatusCode}> (job: {jobId}).");

        return await ReadSuccessResponseJsonAsync<InterlisUploadResponse>(response, cancellationToken);
    }

    private async Task<InterlisStatusResponse> PollStatusAsync(string statusUrl, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var response = await this.httpClient.GetAsync(statusUrl, cancellationToken);
            var statusResponse = await ReadSuccessResponseJsonAsync<InterlisStatusResponse>(response, cancellationToken);

            if (statusResponse.Status == InterlisStatusResponseStatus.Completed
                || statusResponse.Status == InterlisStatusResponseStatus.CompletedWithErrors
                || statusResponse.Status == InterlisStatusResponseStatus.Failed)
            {
                return statusResponse;
            }

            await Task.Delay(this.pollInterval, cancellationToken);
        }

        throw new OperationCanceledException();
    }

    private async Task<Dictionary<LogType, IPipelineFile>> DownloadLogFilesAsync(InterlisStatusResponse statusResponse, CancellationToken cancellationToken)
    {
        var tasks = new List<Task<KeyValuePair<LogType, IPipelineFile>>>();

        if (statusResponse.LogUrl != null)
        {
            tasks.Add(DownloadLogAsFileAsync(statusResponse.LogUrl.ToString(), LogType.ErrorLog, cancellationToken));
        }

        if (statusResponse.XtfLogUrl != null)
        {
            tasks.Add(DownloadLogAsFileAsync(statusResponse.XtfLogUrl.ToString(), LogType.XtfLog, cancellationToken));
        }

        var logFiles = await Task.WhenAll(tasks);
        if (logFiles != null)
        {
            return logFiles.ToDictionary();
        }
        else
        {
            throw new InvalidOperationException("No log files available for download.");
        }
    }

    private async Task<KeyValuePair<LogType, IPipelineFile>> DownloadLogAsFileAsync(string url, LogType logType, CancellationToken cancellationToken)
    {
        IPipelineFile transferFile;
        switch (logType)
        {
            case LogType.ErrorLog:
                transferFile = pipelineFileManager.GeneratePipelineFile("errorLog", "log");
                break;
            case LogType.XtfLog:
                transferFile = pipelineFileManager.GeneratePipelineFile("xtfLog", "xtf");
                break;
            default:
                throw new InvalidOperationException($"Unsupported log type: {logType}");
        }

        using (Stream logDownloadStream = await this.httpClient.GetStreamAsync(url, cancellationToken))
        using (FileStream fileStream = transferFile.OpenWriteFileStream())
        {
            logDownloadStream.CopyTo(fileStream);
        }

        return new KeyValuePair<LogType, IPipelineFile>(logType, transferFile);
    }

    private async Task<T> ReadSuccessResponseJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Invalid response from interlis-check-service");
    }
}
