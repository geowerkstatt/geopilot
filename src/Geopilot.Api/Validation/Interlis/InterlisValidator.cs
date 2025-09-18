using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Immutable;
using System.Net;
using System.Text.Json;

namespace Geopilot.Api.Validation.Interlis;

/// <summary>
/// Validates an INTERLIS file provided through an <see cref="IFileProvider"/>.
/// </summary>
public class InterlisValidator : IValidator
{
    private const string UploadUrl = "/api/v1/upload";
    private const string SettingsUrl = "/api/v1/settings";
    private const string ProfileUrl = "/api/v1/profile";
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(2);

    private readonly ILogger<InterlisValidator> logger;
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private ICollection<string>? supportedFileExtensions;

    private IFileProvider? fileProvider;
    private string? fileName;
    private string? interlisValidationProfile;

    /// <inheritdoc/>
    public string Name => "INTERLIS";

    /// <summary>
    /// Initializes a new instance of the <see cref="InterlisValidator"/> class.
    /// </summary>
    public InterlisValidator(ILogger<InterlisValidator> logger, HttpClient httpClient, IOptions<JsonOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonOptions);

        this.logger = logger;
        this.httpClient = httpClient;
        jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetSupportedFileExtensionsAsync()
    {
        if (supportedFileExtensions != null) return supportedFileExtensions;

        var response = await httpClient.GetAsync(SettingsUrl).ConfigureAwait(false);
        var configResult = await ReadSuccessResponseJsonAsync<InterlisSettingsResponse>(response, CancellationToken.None).ConfigureAwait(false);
        supportedFileExtensions = configResult.AcceptedFileTypes?.Split(", ");
        return supportedFileExtensions ?? Array.Empty<string>();
    }

    /// <summary>
    /// Configures the validator with with everything that is required to execute the validation.
    /// </summary>
    /// <remarks>This method must be called before <see cref="ExecuteAsync(CancellationToken)"/> is called.</remarks>
    /// <param name="fileProvider">A configured <see cref="IFileProvider"/> that can be used to access the file with the specified <paramref name="fileName"/>.</param>
    /// <param name="fileName">The name of the file that should be validated. The file must be accessible through the specified <paramref name="fileProvider"/>.</param>
    /// <param name="interlisValidationProfile">The INTERLIS profile used for validation.</param>
    public void Configure(IFileProvider fileProvider, string fileName, string? interlisValidationProfile)
    {
        ArgumentNullException.ThrowIfNull(fileProvider);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        this.fileProvider = fileProvider;
        this.fileName = fileName;
        this.interlisValidationProfile = interlisValidationProfile;
    }

    /// <inheritdoc/>
    public async Task<ValidatorResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (fileProvider == null) throw new InvalidOperationException("File provider has not been configured.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidOperationException("Transfer file has not been configured.");
        if (!fileProvider.Exists(fileName)) throw new InvalidOperationException($"Transfer file with the specified name <{fileName}> not found.");

        logger.LogInformation("Validating transfer file <{File}>...", fileName);
        var uploadResponse = await UploadTransferFileAsync(fileProvider, fileName, interlisValidationProfile, cancellationToken).ConfigureAwait(false);
        var statusResponse = await PollStatusAsync(uploadResponse.StatusUrl!, cancellationToken).ConfigureAwait(false);
        if (statusResponse == null)
        {
            return new ValidatorResult(ValidatorResultStatus.Failed, "Validation was cancelled.");
        }

        var logFiles = await DownloadLogFilesAsync(statusResponse, fileProvider, fileName, cancellationToken).ConfigureAwait(false);

        return new ValidatorResult(ToValidatorResultStatus(statusResponse.Status), statusResponse.StatusMessage, logFiles.ToImmutableDictionary());
    }

    private async Task<InterlisUploadResponse> UploadTransferFileAsync(IFileProvider fileProvider, string transferFile, string? interlisValidationProfile, CancellationToken cancellationToken)
    {
        if (fileProvider == null)
            throw new InvalidOperationException("Validator not initialized correctly. FileProvider has not been set.");

        using var streamContent = new StreamContent(fileProvider.Open(transferFile));
        using var profileStringContent = new StringContent(interlisValidationProfile ?? string.Empty);
        using var formData = new MultipartFormDataContent
        {
            { streamContent, "file", transferFile },
            { profileStringContent, "profile" },
        };

        using var response = await httpClient.PostAsync(UploadUrl, formData, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            logger.LogError("Upload of transfer file <{TransferFile}> to interlis-check-service failed.", transferFile);
            throw new ValidationFailedException(problemDetails?.Detail ?? "Invalid transfer file");
        }

        logger.LogInformation("Uploaded transfer file <{TransferFile}> to interlis-check-service. Status code <{StatusCode}>.", transferFile, response.StatusCode);

        return await ReadSuccessResponseJsonAsync<InterlisUploadResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InterlisStatusResponse?> PollStatusAsync(string statusUrl, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var response = await httpClient.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var statusResponse = await ReadSuccessResponseJsonAsync<InterlisStatusResponse>(response, cancellationToken).ConfigureAwait(false);

            if (statusResponse.Status == InterlisStatusResponseStatus.Completed
                || statusResponse.Status == InterlisStatusResponseStatus.CompletedWithErrors
                || statusResponse.Status == InterlisStatusResponseStatus.Failed)
            {
                return statusResponse;
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<IDictionary<string, string>> DownloadLogFilesAsync(InterlisStatusResponse statusResponse, IFileProvider fileProvider, string transferFile, CancellationToken cancellationToken)
    {
        var logFiles = new Dictionary<string, string>();
        var tasks = new List<Task>();
        var transferFileWithoutExtension = Path.GetFileNameWithoutExtension(transferFile);

        if (statusResponse.LogUrl != null)
        {
            var logFile = $"{transferFileWithoutExtension}_log.log";
            logFiles["Log"] = logFile;
            tasks.Add(DownloadLogAsFileAsync(fileProvider, statusResponse.LogUrl.ToString(), logFile, cancellationToken));
        }

        if (statusResponse.XtfLogUrl != null)
        {
            var xtfLogFile = $"{transferFileWithoutExtension}_log.xtf";
            logFiles["Xtf-Log"] = xtfLogFile;
            tasks.Add(DownloadLogAsFileAsync(fileProvider, statusResponse.XtfLogUrl.ToString(), xtfLogFile, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return logFiles;
    }

    private async Task DownloadLogAsFileAsync(IFileProvider fileProvider, string url, string destination, CancellationToken cancellationToken)
    {
        using var logDownloadStream = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        using var logFileStream = fileProvider!.CreateFile(destination);
        await logDownloadStream.CopyToAsync(logFileStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ReadSuccessResponseJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Invalid response from interlis-check-service");
    }

    private ValidatorResultStatus ToValidatorResultStatus(InterlisStatusResponseStatus status)
    {
        return status switch
        {
            InterlisStatusResponseStatus.Completed => ValidatorResultStatus.Completed,
            InterlisStatusResponseStatus.CompletedWithErrors => ValidatorResultStatus.CompletedWithErrors,
            InterlisStatusResponseStatus.Failed => ValidatorResultStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    /// <inheritdoc/>
    public async Task<List<Profile>> GetSupportedProfilesAsync()
    {
        try
        {
            var response = await httpClient.GetAsync(ProfileUrl).ConfigureAwait(false);
            var profileList = await ReadSuccessResponseJsonAsync<List<Profile>>(response, CancellationToken.None).ConfigureAwait(false);
            return profileList;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException)
        {
            logger.LogError(ex, "Could not get supported profiles from {InterlisCheckServiceUrl}", httpClient.BaseAddress);
            return new List<Profile>();
        }
    }
}
