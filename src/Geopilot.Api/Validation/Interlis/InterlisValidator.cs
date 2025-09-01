﻿using Geopilot.Api.FileAccess;
using Geowerkstatt.Ilicop.Web.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Geopilot.Api.Validation.Interlis;

/// <summary>
/// Validates an INTERLIS transfer file provided through an <see cref="IFileProvider"/>.
/// </summary>
public class InterlisValidator : IValidator
{
    private const string UploadUrl = "/api/v1/upload";
    private const string SettingsUrl = "/api/v1/settings";
    private const string ProfileUrl = "/api/v1/profile";
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(2);

    private readonly ILogger<InterlisValidator> logger;
    private readonly IFileProvider fileProvider;
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private ICollection<string>? supportedFileExtensions;

    /// <inheritdoc/>
    public string Name => "INTERLIS";

    /// <summary>
    /// Initializes a new instance of the <see cref="InterlisValidator"/> class.
    /// </summary>
    public InterlisValidator(ILogger<InterlisValidator> logger, IFileProvider fileProvider, HttpClient httpClient, IOptions<JsonOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonOptions);

        this.logger = logger;
        this.fileProvider = fileProvider;
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

    /// <inheritdoc/>
    public async Task<ValidatorResult> ExecuteAsync(ValidationJob validationJob, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validationJob);
        if (string.IsNullOrWhiteSpace(validationJob.TempFileName)) throw new ArgumentException("Transfer file name cannot be empty.", nameof(validationJob));

        fileProvider.Initialize(validationJob.Id);
        if (!fileProvider.Exists(validationJob.TempFileName)) throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Transfer file with the specified name <{0}> not found for validation id <{1}>.", validationJob.TempFileName, validationJob.Id));

        logger.LogInformation("Validating transfer file <{File}>...", validationJob.TempFileName);
        var uploadResponse = await UploadTransferFileAsync(validationJob.TempFileName, cancellationToken).ConfigureAwait(false);
        var statusResponse = await PollStatusAsync(uploadResponse.StatusUrl!, cancellationToken).ConfigureAwait(false);
        if (statusResponse == null)
        {
            return new ValidatorResult(Status.Failed, "Validation was cancelled.");
        }

        var logFiles = await DownloadLogFilesAsync(statusResponse, validationJob.TempFileName, cancellationToken).ConfigureAwait(false);

        return new ValidatorResult(statusResponse.Status, statusResponse.StatusMessage)
        {
            LogFiles = logFiles,
        };
    }

    private async Task<InterlisUploadResponse> UploadTransferFileAsync(string transferFile, CancellationToken cancellationToken)
    {
        using var streamContent = new StreamContent(fileProvider.Open(transferFile));
        using var formData = new MultipartFormDataContent { { streamContent, "file", transferFile } };
        using var response = await httpClient.PostAsync(UploadUrl, formData, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Uploaded transfer file <{TransferFile}> to interlis-check-service. Status code <{StatusCode}>.", transferFile, response.StatusCode);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            throw new ValidationFailedException(problemDetails?.Detail ?? "Invalid transfer file");
        }

        return await ReadSuccessResponseJsonAsync<InterlisUploadResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InterlisStatusResponse?> PollStatusAsync(string statusUrl, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var response = await httpClient.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var statusResponse = await ReadSuccessResponseJsonAsync<InterlisStatusResponse>(response, cancellationToken).ConfigureAwait(false);

            if (statusResponse.Status == Status.Completed
                || statusResponse.Status == Status.CompletedWithErrors
                || statusResponse.Status == Status.Failed)
            {
                return statusResponse;
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<IDictionary<string, string>> DownloadLogFilesAsync(InterlisStatusResponse statusResponse, string transferFile, CancellationToken cancellationToken)
    {
        var logFiles = new Dictionary<string, string>();
        var tasks = new List<Task>();
        var transferFileWithoutExtension = Path.GetFileNameWithoutExtension(transferFile);

        if (statusResponse.LogUrl != null)
        {
            var logFile = $"{transferFileWithoutExtension}_log.log";
            logFiles["Log"] = logFile;
            tasks.Add(DownloadLogAsFileAsync(statusResponse.LogUrl.ToString(), logFile, cancellationToken));
        }

        if (statusResponse.XtfLogUrl != null)
        {
            var xtfLogFile = $"{transferFileWithoutExtension}_log.xtf";
            logFiles["Xtf-Log"] = xtfLogFile;
            tasks.Add(DownloadLogAsFileAsync(statusResponse.XtfLogUrl.ToString(), xtfLogFile, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return logFiles;
    }

    private async Task DownloadLogAsFileAsync(string url, string destination, CancellationToken cancellationToken)
    {
        using var logDownloadStream = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        using var logFileStream = fileProvider.CreateFile(destination);
        await logDownloadStream.CopyToAsync(logFileStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ReadSuccessResponseJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Invalid response from interlis-check-service");
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
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not get supported profiles from {InterlisCheckServiceUrl}", httpClient.BaseAddress);
            return new List<Profile>();
        }
    }
}
