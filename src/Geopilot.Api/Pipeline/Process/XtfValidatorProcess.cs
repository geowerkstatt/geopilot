using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Test.Pipeline;
using Geopilot.Api.Validation;
using Geopilot.Api.Validation.Interlis;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process;

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
    private const string ConfiguratiionKeyValidationProfile = "profile";
    private const string ConfiguratiionKeyPollInterval = "poll_interval";
    private const string InterlisCheckServiceBaseAddressConfiguration = "Validation:InterlisCheckServiceUrl";
    private const string UploadUrl = "/api/v1/upload";

    private static readonly JsonSerializerOptions JsonOptions;

    private ILogger<XtfValidatorProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<XtfValidatorProcess>();

    private Parameterization config = new Parameterization();

    private HttpClient httpClient = new();

    static XtfValidatorProcess()
    {
        JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="XtfValidatorProcess"/>.
    /// </summary>
    [PipelineProcessCleanup]
    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    /// <summary>
    /// Initializes the pipeline process with the specified configuration settings.
    /// </summary>
    /// <param name="config">A dictionary containing configuration key-value pairs to be used for initialization. Cannot be null.<para>'profile': optional profile to run the validation with.</para><para>'poll_interval': optional polling interval for the validation process.</para></param>
    /// <param name="configuration">The configuration source used to retrieve the base address for the INTERLIS check service. Cannot be null and
    /// must contain a valid service URL at value "Validation:InterlisCheckServiceUrl".</param>
    /// <exception cref="InvalidOperationException">Thrown if the configuration does not provide a valid INTERLIS check service base address.</exception>
    [PipelineProcessInitialize]
    public void Initialize(Parameterization config, IConfiguration configuration)
    {
        this.config = config;

        var checkServiceUrl = configuration.GetValue<string>(InterlisCheckServiceBaseAddressConfiguration) ?? throw new InvalidOperationException("Missing InterlisCheckServiceUrl to validate INTERLIS transfer files.");
        this.httpClient.BaseAddress = new Uri(checkServiceUrl);

        this.httpClient.DefaultRequestHeaders.Accept.Clear();
        this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string Profile
    {
        get
        {
            if (this.config.TryGetValue(ConfiguratiionKeyValidationProfile, out var profile))
                return profile;
            else
                return string.Empty;
        }
    }

    private TimeSpan PollInterval
    {
        get
        {
            if (this.config != null && this.config.TryGetValue(ConfiguratiionKeyPollInterval, out var pollIntervalStr) && int.TryParse(pollIntervalStr, out var pollInterval))
                return TimeSpan.FromMilliseconds(pollInterval);
            else
                return TimeSpan.FromSeconds(2);
        }
    }

    /// <summary>
    /// Runs the validation process for the specified ILI file.
    /// </summary>
    /// <param name="iliFile">The ILI file to validate. Cannot be null.</param>
    /// <param name="cancellationToken">Cancellation token to canclel the operation.</param>
    /// <returns>A ProcessData instance containing the results of the validation process.</returns>
    /// <exception cref="ArgumentException">Thrown if the input ILI file is invalid.</exception>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object>> RunAsync(IPipelineTransferFile iliFile, CancellationToken cancellationToken)
    {
        logger.LogInformation("Validating transfer file <{File}>...", iliFile.FileName);
        var uploadResponse = await UploadTransferFileAsync(iliFile, iliFile.FileName, this.Profile, cancellationToken);
        var statusResponse = await PollStatusAsync(uploadResponse.StatusUrl!, cancellationToken);
        var logFiles = await DownloadLogFilesAsync(statusResponse, cancellationToken);

        return new Dictionary<string, object>()
        {
            { OutputMappingErrorLog, logFiles[LogType.ErrorLog] },
            { OutputMappingXtfLog, logFiles[LogType.XtfLog] },
        };
    }

    private async Task<InterlisUploadResponse> UploadTransferFileAsync(IPipelineTransferFile file, string transferFile, string? interlisValidationProfile, CancellationToken cancellationToken)
    {
        using var fileStream = file.OpenFileStream() ?? throw new ArgumentException("Invalid input ILI file stream.");
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
            logger.LogError("Upload of transfer file <{TransferFile}> to interlis-check-service failed.", transferFile);
            throw new ValidationFailedException(problemDetails?.Detail ?? "Invalid transfer file");
        }

        logger.LogInformation("Uploaded transfer file <{TransferFile}> to interlis-check-service. Status code <{StatusCode}>.", transferFile, response.StatusCode);

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

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new OperationCanceledException();
    }

    private async Task<Dictionary<LogType, IPipelineTransferFile>> DownloadLogFilesAsync(InterlisStatusResponse statusResponse, CancellationToken cancellationToken)
    {
        var tasks = new List<Task<KeyValuePair<LogType, IPipelineTransferFile>>>();

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

    private async Task<KeyValuePair<LogType, IPipelineTransferFile>> DownloadLogAsFileAsync(string url, LogType logType, CancellationToken cancellationToken)
    {
        PipelineTransferFile transferFile;
        switch (logType)
        {
            case LogType.ErrorLog:
                transferFile = new PipelineTransferFile("errorLog", Path.GetTempFileName().Replace(".tmp", ".log"));
                break;
            case LogType.XtfLog:
                transferFile = new PipelineTransferFile("xtfLog", Path.GetTempFileName().Replace(".tmp", ".xtf"));
                break;
            default:
                throw new InvalidOperationException($"Unsupported log type: {logType}");
        }

        using var logDownloadStream = await this.httpClient.GetStreamAsync(url, cancellationToken);
        var fileStream = File.Create(transferFile.FilePath);
        logDownloadStream.Seek(0, SeekOrigin.Begin);
        logDownloadStream.CopyTo(fileStream);
        fileStream.Close();

        return new KeyValuePair<LogType, IPipelineTransferFile>(logType, transferFile);
    }

    private async Task<T> ReadSuccessResponseJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Invalid response from interlis-check-service");
    }
}
