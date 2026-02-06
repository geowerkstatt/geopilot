using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Validation;
using Geopilot.Api.Validation.Interlis;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Process for validating ILI files.
/// </summary>
internal class IliValidatorProcess : IPipelineProcess, IDisposable
{
    private const string InputMappingIliFile = "ili_file";
    private const string OutputMappingErrorLog = "error_log";
    private const string OutputMappingXtfLog = "xtf_log";
    private const string InterlisCheckServiceBaseAddressConfiguration = "Validation:InterlisCheckServiceUrl";
    private const string UploadUrl = "/api/v1/upload";
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private ILogger<IliValidatorProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<IliValidatorProcess>();

    /// <inheritdoc/>
    public required string Name { get; set; }

    /// <inheritdoc/>
    public required DataHandlingConfig DataHandlingConfig { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, string>? Config { get; set; }

    private HttpClient httpClient = new();
    private CancellationToken? cancellationToken;

    /// <summary>
    /// Disposes the resources used by the <see cref="IliValidatorProcess"/>.
    /// </summary>
    public void Dispose()
    {
        httpClient?.Dispose();
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
        httpClient.BaseAddress = new Uri(checkServiceUrl);

        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
        var inputIliFileKey = DataHandlingConfig.GetInputMapping(InputMappingIliFile);
        if (inputIliFileKey == null || !inputData.Data.TryGetValue(inputIliFileKey, out var iliFilePart))
        {
            var errorMessage = $"IliValidatorProcess: input data does not contain required key '{InputMappingIliFile}'.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        return iliFilePart;
    }

    /// <inheritdoc/>
    public ProcessData Run(ProcessData inputData)
    {
        var inputIliFile = InputIliFile(inputData).Data as FileHandle ?? throw new ArgumentException("Invalid input ILI file.");

        var outputData = new ProcessData();

        logger.LogInformation("Validating transfer file <{File}>...", inputIliFile.FileName);
        var uploadResponse = Task.Run(() => UploadTransferFileAsync(inputIliFile, inputIliFile.FileName, "")).GetAwaiter().GetResult();
        var statusResponse = Task.Run(() => PollStatusAsync(uploadResponse.StatusUrl!)).GetAwaiter().GetResult();
        var logFiles = Task.Run(() => DownloadLogFilesAsync(statusResponse, "fileName")).GetAwaiter().GetResult();

        outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingErrorLog), new ProcessDataPart(logFiles["Log"]));
        outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingXtfLog), new ProcessDataPart(logFiles["Xtf-Log"]));

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

        var token = cancellationToken ?? throw new ArgumentException("Invalid input cancellation token.");

        using var response = await httpClient.PostAsync(UploadUrl, formData, token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions, token).ConfigureAwait(false);
            logger.LogError("Upload of transfer file <{TransferFile}> to interlis-check-service failed.", transferFile);
            throw new ValidationFailedException(problemDetails?.Detail ?? "Invalid transfer file");
        }

        logger.LogInformation("Uploaded transfer file <{TransferFile}> to interlis-check-service. Status code <{StatusCode}>.", transferFile, response.StatusCode);

        return await ReadSuccessResponseJsonAsync<InterlisUploadResponse>(response, token).ConfigureAwait(false);
    }

    private async Task<InterlisStatusResponse> PollStatusAsync(string statusUrl)
    {
        var token = cancellationToken ?? throw new ArgumentException("Invalid input cancellation token.");
        while (!token.IsCancellationRequested)
        {
            using var response = await httpClient.GetAsync(statusUrl, token).ConfigureAwait(false);
            var statusResponse = await ReadSuccessResponseJsonAsync<InterlisStatusResponse>(response, token).ConfigureAwait(false);

            if (statusResponse.Status == InterlisStatusResponseStatus.Completed
                || statusResponse.Status == InterlisStatusResponseStatus.CompletedWithErrors
                || statusResponse.Status == InterlisStatusResponseStatus.Failed)
            {
                return statusResponse;
            }

            await Task.Delay(pollInterval, token).ConfigureAwait(false);
        }

        throw new OperationCanceledException();
    }

    private async Task<IDictionary<string, string>> DownloadLogFilesAsync(InterlisStatusResponse statusResponse, string transferFile)
    {
        var logFiles = new Dictionary<string, string>();
        var tasks = new List<Task>();
        var transferFileWithoutExtension = Path.GetFileNameWithoutExtension(transferFile);

        if (statusResponse.LogUrl != null)
        {
            var logFile = $"{transferFileWithoutExtension}_log.log";
            logFiles["Log"] = logFile;
            tasks.Add(DownloadLogAsFileAsync(statusResponse.LogUrl.ToString(), logFile));
        }

        if (statusResponse.XtfLogUrl != null)
        {
            var xtfLogFile = $"{transferFileWithoutExtension}_log.xtf";
            logFiles["Xtf-Log"] = xtfLogFile;
            tasks.Add(DownloadLogAsFileAsync(statusResponse.XtfLogUrl.ToString(), xtfLogFile));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return logFiles;
    }

    private async Task<string> DownloadLogAsFileAsync(string url, string destination)
    {
        var token = cancellationToken ?? throw new ArgumentException("Invalid input cancellation token.");
        using var logDownloadStream = await httpClient.GetStreamAsync(url, token).ConfigureAwait(false);
        using (var reader = new StreamReader(logDownloadStream))
        {
            return await reader.ReadToEndAsync(token);
        }
    }

    private async Task<T> ReadSuccessResponseJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Invalid response from interlis-check-service");
    }
}
