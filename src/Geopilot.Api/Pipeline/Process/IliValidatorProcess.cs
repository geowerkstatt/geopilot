using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;
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
internal class IliValidatorProcess : IPipelineProcess, IDisposable
{
    private enum LogType
    {
        XtfLog,
        ErrorLog,
    }

    private const string InputMappingIliFile = "ili_file";
    private const string OutputMappingErrorLog = "error_log";
    private const string OutputMappingXtfLog = "xtf_log";
    private const string ConfiguratiionKeyValidationProfile = "profile";
    private const string ConfiguratiionKeyPollInterval = "poll_interval";
    private const string InterlisCheckServiceBaseAddressConfiguration = "Validation:InterlisCheckServiceUrl";
    private const string UploadUrl = "/api/v1/upload";

    private static readonly JsonSerializerOptions JsonOptions;

    private ILogger<IliValidatorProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<IliValidatorProcess>();

    private DataHandlingConfig? DataHandlingConfig { get; set; }

    private IDictionary<string, string>? Config { get; set; }

    private HttpClient HttpClient { get; set; } = new();

    private CancellationToken? cancellationToken;

    static IliValidatorProcess()
    {
        JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="IliValidatorProcess"/>.
    /// </summary>
    public void Dispose()
    {
        this.HttpClient.Dispose();
    }

    /// <summary>
    /// Initializes the pipeline process with the specified configuration settings.
    /// </summary>
    /// <param name="config">A dictionary containing configuration key-value pairs to be used for initialization. Cannot be null.</param>
    /// <remarks>The configuration settings provided in the dictionary will be stored in the process for later use during execution.
    /// <para>'profile': optional profile to run the validation with.</para>
    /// <para>'poll_interval': optional polling interval for the validation process.</para>
    /// </remarks>
    [PipelineProcessInitialize]
    public void Initialize(IDictionary<string, string> config)
    {
        this.Config = config;
    }

    /// <summary>
    /// Initializes the pipeline process with the specified data handling configuration.
    /// </summary>
    /// <param name="dataHandlingConfig">The data handling configuration to be used for the pipeline process. Cannot be null.</param>
    [PipelineProcessInitialize]
    public void Initialize(DataHandlingConfig dataHandlingConfig)
    {
        this.DataHandlingConfig = dataHandlingConfig;
    }

    /// <summary>
    /// Initializes the pipeline process by configuring the HTTP client with the base address and default request
    /// headers required for INTERLIS transfer file validation.
    /// </summary>
    /// <remarks>This method must be called before performing any operations that require communication with
    /// the INTERLIS check service. The HTTP client will be set to accept JSON responses from the service.</remarks>
    /// <param name="configuration">The configuration source used to retrieve the base address for the INTERLIS check service. Cannot be null and
    /// must contain a valid service URL at value "Validation:InterlisCheckServiceUrl".</param>
    /// <exception cref="InvalidOperationException">Thrown if the configuration does not provide a valid INTERLIS check service base address.</exception>
    [PipelineProcessInitialize]
    public void Initialize(IConfiguration configuration)
    {
        var checkServiceUrl = configuration.GetValue<string>(InterlisCheckServiceBaseAddressConfiguration)
            ?? throw new InvalidOperationException("Missing InterlisCheckServiceUrl to validate INTERLIS transfer files.");
        this.HttpClient.BaseAddress = new Uri(checkServiceUrl);

        this.HttpClient.DefaultRequestHeaders.Accept.Clear();
        this.HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Initializes the pipeline process by configuring the cancellation token.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to be used for the pipeline process.</param>
    [PipelineProcessInitialize]
    public void Initialize(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
    }

    private ProcessDataPart InputIliFile(ProcessData inputData)
    {
        var inputIliFileKey = DataHandlingConfig?.GetInputMapping(InputMappingIliFile);
        if (inputIliFileKey == null || !inputData.Data.TryGetValue(inputIliFileKey, out var iliFilePart))
        {
            var errorMessage = $"IliValidatorProcess: input data does not contain required key '{InputMappingIliFile}'.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        return iliFilePart;
    }

    private CancellationToken CancellationToken
    {
        get
        {
            return cancellationToken ?? CancellationToken.None;
        }
    }

    private string Profile
    {
        get
        {
            if (this.Config != null && this.Config.TryGetValue(ConfiguratiionKeyValidationProfile, out var profile))
                return profile;
            else
                return string.Empty;
        }
    }

    private TimeSpan PollInterval
    {
        get
        {
            if (this.Config != null && this.Config.TryGetValue(ConfiguratiionKeyPollInterval, out var pollIntervalStr) && int.TryParse(pollIntervalStr, out var pollInterval))
                return TimeSpan.FromMilliseconds(pollInterval);
            else
                return TimeSpan.FromSeconds(2);
        }
    }

    /// <inheritdoc/>
    public ProcessData Run(ProcessData inputData)
    {
        var inputIliFile = InputIliFile(inputData).Data as FileHandle ?? throw new ArgumentException("Invalid input ILI file.");

        var outputData = new ProcessData();

        logger.LogInformation("Validating transfer file <{File}>...", inputIliFile.FileName);
        var uploadResponse = Task.Run(() => UploadTransferFileAsync(inputIliFile, inputIliFile.FileName, this.Profile)).GetAwaiter().GetResult();
        var statusResponse = Task.Run(() => PollStatusAsync(uploadResponse.StatusUrl!)).GetAwaiter().GetResult();
        var logFiles = Task.Run(() => DownloadLogFilesAsync(statusResponse)).GetAwaiter().GetResult();

        if (DataHandlingConfig != null)
        {
            outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingErrorLog), new ProcessDataPart(logFiles[LogType.ErrorLog]));
            outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingXtfLog), new ProcessDataPart(logFiles[LogType.XtfLog]));
        }
        else
        {
            var errorMsg = "DataHandlingConfig is not set. Output data will not be mapped to any keys.";
            logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg);
        }

        return outputData;
    }

    private async Task<InterlisUploadResponse> UploadTransferFileAsync(FileHandle fileHandle, string transferFile, string? interlisValidationProfile)
    {
        using var fileStream = fileHandle.Stream ?? throw new ArgumentException("Invalid input ILI file stream.");
        using var streamContent = new StreamContent(fileStream);
        using var profileStringContent = new StringContent(interlisValidationProfile ?? string.Empty);
        using var formData = new MultipartFormDataContent
        {
            { streamContent, "file", transferFile },
            { profileStringContent, "profile" },
        };

        using var response = await this.HttpClient.PostAsync(UploadUrl, formData, CancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions, CancellationToken).ConfigureAwait(false);
            logger.LogError("Upload of transfer file <{TransferFile}> to interlis-check-service failed.", transferFile);
            throw new ValidationFailedException(problemDetails?.Detail ?? "Invalid transfer file");
        }

        logger.LogInformation("Uploaded transfer file <{TransferFile}> to interlis-check-service. Status code <{StatusCode}>.", transferFile, response.StatusCode);

        return await ReadSuccessResponseJsonAsync<InterlisUploadResponse>(response).ConfigureAwait(false);
     }

    private async Task<InterlisStatusResponse> PollStatusAsync(string statusUrl)
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            using var response = await this.HttpClient.GetAsync(statusUrl, CancellationToken).ConfigureAwait(false);
            var statusResponse = await ReadSuccessResponseJsonAsync<InterlisStatusResponse>(response).ConfigureAwait(false);

            if (statusResponse.Status == InterlisStatusResponseStatus.Completed
                || statusResponse.Status == InterlisStatusResponseStatus.CompletedWithErrors
                || statusResponse.Status == InterlisStatusResponseStatus.Failed)
            {
                return statusResponse;
            }

            await Task.Delay(PollInterval, CancellationToken).ConfigureAwait(false);
        }

        throw new OperationCanceledException();
    }

    private async Task<Dictionary<LogType, string>> DownloadLogFilesAsync(InterlisStatusResponse statusResponse)
    {
        List<Task<KeyValuePair<LogType, string>>> tasks = new List<Task<KeyValuePair<LogType, string>>>();

        if (statusResponse.LogUrl != null)
        {
            tasks.Add(DownloadLogAsFileAsync(statusResponse.LogUrl.ToString(), LogType.ErrorLog));
        }

        if (statusResponse.XtfLogUrl != null)
        {
            tasks.Add(DownloadLogAsFileAsync(statusResponse.XtfLogUrl.ToString(), LogType.XtfLog));
        }

        var logFiles = await Task.WhenAll(tasks).ConfigureAwait(false);
        if (logFiles != null)
        {
            return logFiles.ToDictionary();
        }
        else
        {
            throw new InvalidOperationException("No log files available for download.");
        }
    }

    private async Task<KeyValuePair<LogType, string>> DownloadLogAsFileAsync(string url, LogType logType)
    {
        using var logDownloadStream = await this.HttpClient.GetStreamAsync(url, CancellationToken).ConfigureAwait(false);
        using (var reader = new StreamReader(logDownloadStream))
        {
            return new KeyValuePair<LogType, string>(logType, await reader.ReadToEndAsync(CancellationToken));
        }
    }

    private async Task<T> ReadSuccessResponseJsonAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, CancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Invalid response from interlis-check-service");
    }
}
