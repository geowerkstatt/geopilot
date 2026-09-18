using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Machine based delivery: one call hands geopilot a mandate, the delivery details and the data, and the
/// attempt is then polled until it is delivered or refused. The resource exists only where the installation
/// offers the capability (<c>MachineDelivery:Enabled</c>).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class SubmissionController : ControllerBase
{
    /// <summary>
    /// What a single form field of the multipart form may take. The fields carry a mandate key, a flag, an id
    /// and a comment; without a bound of their own they would be read into memory up to the whole job size,
    /// which is dimensioned for file content.
    /// </summary>
    private const int MaxFormFieldBytes = 8 * 1024;

    /// <summary>
    /// What a caller refused for want of capacity is told to wait. Deliberately a constant: the queue is not
    /// observable from here, and a made-up estimate would be worse than a plain "not now".
    /// </summary>
    private const int RetryAfterSeconds = 60;

    private readonly ILogger<SubmissionController> logger;
    private readonly Context context;
    private readonly IMandateService mandateService;
    private readonly IProcessingService processingService;
    private readonly ISubmissionStore submissionStore;
    private readonly IUploadStore uploadStore;
    private readonly IUploadStorage uploadStorage;
    private readonly IUploadOrchestrationService uploadOrchestration;
    private readonly IDownloadFileStore downloadFileStore;
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly UploadOptions uploadOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionController"/> class.
    /// </summary>
    public SubmissionController(
        ILogger<SubmissionController> logger,
        Context context,
        IMandateService mandateService,
        IProcessingService processingService,
        ISubmissionStore submissionStore,
        IUploadStore uploadStore,
        IUploadStorage uploadStorage,
        IUploadOrchestrationService uploadOrchestration,
        IDownloadFileStore downloadFileStore,
        IContentTypeProvider contentTypeProvider,
        IOptions<UploadOptions> uploadOptions)
    {
        ArgumentNullException.ThrowIfNull(uploadOptions);

        this.logger = logger;
        this.context = context;
        this.mandateService = mandateService;
        this.processingService = processingService;
        this.submissionStore = submissionStore;
        this.uploadStore = uploadStore;
        this.uploadStorage = uploadStorage;
        this.uploadOrchestration = uploadOrchestration;
        this.downloadFileStore = downloadFileStore;
        this.contentTypeProvider = contentTypeProvider;
        this.uploadOptions = uploadOptions.Value;
    }

    /// <summary>
    /// Starts a machine delivery for files that were uploaded beforehand. This is the form an installation
    /// expects that keeps its uploads outside the API, where the client obtains the upload and its URLs first.
    /// </summary>
    /// <param name="request">The mandate, the upload and the delivery details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Authorize(Policy = GeopilotPolicies.User)]
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status202Accepted, "The attempt was accepted and is being processed.", typeof(SubmissionResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "This installation takes the files with the request, the upload has no file with a file extension, or the delivery details violate the rules of the mandate.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The caller is not authorized.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No mandate with the given key is accessible, or the upload does not exist.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The mandate cannot take a delivery.")]
    public async Task<IActionResult> Create(SubmissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (uploadOptions.Backend != UploadBackend.Cloud)
        {
            return BadRequest("This installation takes the files with the request. Send them as multipart/form-data to api/v1/submission/files instead of referencing an upload.");
        }

        var user = await context.GetUserByPrincipalAsync(User);

        // The key is free text from the request: it may be reflected to its sender, but it must not reach the log.
        var mandate = await mandateService.GetMandateByKeyForUser(request.MandateKey, user);
        if (mandate is null)
        {
            logger.LogInformation("A machine delivery was refused because no accessible mandate carries the given key.");
            return NotFound($"No mandate with key <{request.MandateKey}> found.");
        }

        MandateDeliverability deliverability;
        try
        {
            deliverability = await mandateService.GetDeliverabilityAsync(mandate, request.UploadId);
        }
        catch (ArgumentException)
        {
            logger.LogInformation("A machine delivery was refused because upload <{UploadId}> is unknown.", request.UploadId);
            return NotFound($"No upload with id <{request.UploadId}> found.");
        }
        catch (InvalidOperationException)
        {
            // The upload exists but none of its files carries an extension, so its file types cannot be checked.
            logger.LogInformation("A machine delivery was refused because upload <{UploadId}> has no file with a file extension.", request.UploadId);
            return BadRequest($"Upload <{request.UploadId}> has no file with a file extension, so its file types cannot be checked against the mandate.");
        }

        switch (deliverability)
        {
            case MandateDeliverability.DeliveryNotAllowed:
                return Conflict($"Mandate <{request.MandateKey}> does not accept deliveries.");
            case MandateDeliverability.PipelineNotConfigured:
                return Conflict($"Mandate <{request.MandateKey}> names no pipeline this installation offers, which is a configuration issue of the installation.");
            case MandateDeliverability.FilesNotAccepted:
                return BadRequest($"Mandate <{request.MandateKey}> does not accept all file types of the upload.");
            default:
                break;
        }

        var fields = new DeliveryFields(request.PartialDelivery, request.PrecursorDeliveryId, request.Comment);
        var fieldErrors = await ValidateDeliveryFieldsAsync(mandate.Id, fields, cancellationToken);
        if (fieldErrors.Count > 0)
        {
            foreach (var fieldError in fieldErrors)
            {
                foreach (var message in fieldError.Value)
                {
                    ModelState.AddModelError(fieldError.Key, message);
                }
            }

            return ValidationProblem(ModelState);
        }

        // Started on the request thread: the execution protocol classifies the client from the current request,
        // and without it every machine delivery would be recorded as coming from an unknown client.
        var job = await processingService.StartJobAsync(request.UploadId, mandate.Id, user);

        // The stored key is the one the mandate carries, not the one the request spelled: both routes echo the
        // same value back, whatever whitespace the caller sent.
        var submission = new Submission(job.Id, mandate.Key ?? string.Empty, user.Id, fields);
        submissionStore.Add(submission);

        logger.LogInformation("Accepted machine delivery <{JobId}> for mandate <{MandateId}>.", job.Id, mandate.Id);
        return Accepted(BuildStatusUrl(job.Id), BuildResponse(job, submission));
    }

    /// <summary>
    /// Starts a machine delivery whose files come with the request. This is the form an installation expects
    /// that writes uploads through the API itself. The form fields must precede the files, so a wrong mandate
    /// or a missing delivery detail is refused before any file content is read.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Authorize(Policy = GeopilotPolicies.User)]
    [HttpPost("files")]
    [Consumes("multipart/form-data")]
    [SelfManagedBodySize]
    [DisableFormValueModelBinding]
    [SwaggerResponse(StatusCodes.Status202Accepted, "The attempt was accepted and is being processed.", typeof(SubmissionResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "This installation expects the files to be uploaded beforehand, or the delivery details violate the rules of the mandate.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The caller is not authorized.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No mandate with the given key is accessible.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The mandate cannot take a delivery.")]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "A file is larger than this installation accepts.")]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "This installation is at its upload capacity. Retry after the time the Retry-After header names.")]
    public async Task<IActionResult> CreateWithFiles(CancellationToken cancellationToken)
    {
        // Only a backend that writes through the API can take the files from the request itself.
        if (uploadStorage is not DirectUploadStorage directStorage)
        {
            return BadRequest("This installation stores uploads outside the API. Upload the files first and reference the upload id in a JSON request to api/v1/submission.");
        }

        var boundary = ReadBoundary();
        if (boundary is null)
        {
            return BadRequest("The request is not a valid multipart form.");
        }

        var atCapacity = await RefuseWhileAtCapacityAsync();
        if (atCapacity is not null)
            return atCapacity;

        RaiseBodySizeLimit();

        var user = await context.GetUserByPrincipalAsync(User);
        var uploadId = Guid.NewGuid();
        var storedFiles = new List<UploadedFileInfo>();
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Set when the first file part arrives, which is the moment the fields are complete and checked.
        (Mandate Mandate, DeliveryFields Fields)? declaration = null;
        var accepted = false;

        try
        {
            var reader = new MultipartReader(boundary, Request.Body)
            {
                // Bound what a single part may write before it is refused: without this a file over the limit
                // is stored in full and only then compared, so it costs the disk space anyway. The exact limit
                // is still enforced on the stored size. Never below the field limit, so the fields stay
                // readable on an installation whose file limit is smaller than a field.
                BodyLengthLimit = Math.Max((long)uploadOptions.MaxFileSizeMB * 1024 * 1024, MaxFormFieldBytes + 1),
            };

            for (var section = await ReadSectionAsync(reader, cancellationToken); section is not null; section = await ReadSectionAsync(reader, cancellationToken))
            {
                var disposition = section.GetContentDispositionHeader();
                if (disposition is null)
                    continue;

                if (disposition.IsFormDisposition())
                {
                    var name = disposition.Name.Value ?? string.Empty;

                    // The fields are read and checked before the first file, so one arriving afterwards would
                    // have no effect at all. Refusing it beats accepting a delivery without the detail it carried.
                    if (declaration is not null)
                        return BadRequest($"The form field <{name}> arrived after a file. Send every field before the files.");

                    var value = await ReadFieldAsync(section, cancellationToken);
                    if (value is null)
                        return BadRequest($"The form field <{name}> is longer than the {MaxFormFieldBytes} bytes a delivery detail may take.");

                    fields[name] = value;
                    continue;
                }

                if (!disposition.IsFileDisposition())
                    continue;

                if (declaration is null)
                {
                    var (resolvedMandate, resolvedFields, failure) = await ResolveMandateAsync(fields, user, cancellationToken);
                    if (resolvedMandate is null || resolvedFields is null)
                        return failure ?? BadRequest("The mandate and the delivery details could not be read from the request.");

                    declaration = (resolvedMandate, resolvedFields);
                }

                var fileFailure = await StoreFileAsync(section, disposition, declaration.Value.Mandate, directStorage, uploadId, storedFiles, cancellationToken);
                if (fileFailure is not null)
                    return fileFailure;
            }

            if (declaration is null || storedFiles.Count == 0)
            {
                return BadRequest("The request carries no files. Send the mandate and the delivery details as form fields, followed by the files.");
            }

            var (mandate, deliveryFields) = declaration.Value;
            uploadStore.CreateUpload(uploadId, storedFiles.ToImmutableList());

            // The job is started before the attempt is remembered, because the job id is the identity of the
            // attempt. Nothing can finish the job in between: preflight, scanning and the run all happen on
            // other threads after this call returns.
            var job = await processingService.StartJobAsync(uploadId, mandate.Id, user);
            var submission = new Submission(job.Id, mandate.Key ?? string.Empty, user.Id, deliveryFields);
            submissionStore.Add(submission);
            accepted = true;

            logger.LogInformation("Accepted machine delivery <{JobId}> with {FileCount} files for mandate <{MandateId}>.", job.Id, storedFiles.Count, mandate.Id);
            return Accepted(BuildStatusUrl(job.Id), BuildResponse(job, submission));
        }
        catch (Exception ex) when (ex is InvalidDataException or RequestBodyException)
        {
            // Both are the caller's to fix: a body the multipart reader cannot make sense of, and one that ended
            // before it said it would. A server error is reserved for what the installation can fix, which is why
            // the storage keeps raising its own IOException instead of being folded in here. The one size case is
            // answered as 413 where the file is stored.
            logger.LogInformation(ex, "A machine delivery was refused because its multipart body could not be read.");
            return BadRequest("The multipart body could not be read.");
        }
        finally
        {
            // Whatever landed for a refused or failed attempt is nobody's data. Released through the
            // orchestration, because the upload session has to go with the files: left behind it would count
            // against MaxActiveJobs until the age-based sweep runs and crowd out real callers. The release
            // swallows and logs its own errors, so it cannot mask the failure that brought us here.
            if (!accepted)
                await uploadOrchestration.ReleaseUploadAsync(uploadId);
        }
    }

    /// <summary>
    /// Gets the state of a machine delivery attempt. The attempt is readable while its job lives; afterwards
    /// the delivery it created is the durable record.
    /// </summary>
    /// <param name="id">The id of the attempt.</param>
    [Authorize(Policy = GeopilotPolicies.User)]
    [HttpGet("{id}", Name = nameof(GetSubmissionStatus))]
    [SwaggerResponse(StatusCodes.Status200OK, "The attempt was found.", typeof(SubmissionResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The caller is not authorized.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "This process knows no such attempt. It never existed, it expired with its job, or it belongs to another caller.")]
    public async Task<IActionResult> GetSubmissionStatus(Guid id)
    {
        var (submission, job) = await ResolveAttemptAsync(id);
        if (submission is null || job is null)
            return NotFound($"No submission with id <{id}> found.");

        return Ok(BuildResponse(job, submission));
    }

    /// <summary>
    /// Downloads a file the run offers, whatever one of its steps tagged for download.
    /// </summary>
    /// <param name="id">The id of the attempt.</param>
    /// <param name="name">The storage name of the file, taken from the <c>url</c> of a download listed on the attempt. That URL, not this name, is what a client follows.</param>
    [Authorize(Policy = GeopilotPolicies.User)]
    [HttpGet("{id}/downloads/{name}", Name = nameof(GetDownload))]
    [SwaggerResponse(StatusCodes.Status200OK, "The file was found.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The caller is not authorized.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The attempt or the file cannot be found.")]
    public async Task<IActionResult> GetDownload(Guid id, string name)
    {
        var (submission, job) = await ResolveAttemptAsync(id);
        if (submission is null)
            return NotFound($"No submission with id <{id}> found.");

        if (!downloadFileStore.Exists(id, name))
            return NotFound($"No file <{name}> found for submission <{id}>.");

        var stream = downloadFileStore.OpenFile(id, name);

        // Served under the name the step gave it, the one the attempt lists, not the storage name that keeps
        // two steps from colliding. ProcessingController does the same for the web interface.
        var downloadName = ResolveOriginalFileName(job, name) ?? Path.GetFileName(name);
        return File(stream, contentTypeProvider.GetContentTypeAsString(name), downloadName);
    }

    private static string? ResolveOriginalFileName(ProcessingJob? job, string persistedFileName)
        => job?.Pipeline?.Steps
            .SelectMany(step => step.Downloads)
            .FirstOrDefault(file => file.PersistedFileName == persistedFileName)
            ?.OriginalFileName;

    private async Task<(Submission? Submission, ProcessingJob? Job)> ResolveAttemptAsync(Guid id)
    {
        var submission = submissionStore.GetSubmission(id);
        if (submission is null)
            return (null, null);

        // An attempt belongs to the caller that started it; another caller must not learn its state.
        var user = await context.GetUserByPrincipalAsync(User);
        if (submission.DeclaringUserId != user.Id)
            return (null, null);

        return (submission, processingService.GetJob(id));
    }

    private async Task<Dictionary<string, string[]>> ValidateDeliveryFieldsAsync(int mandateId, DeliveryFields fields, CancellationToken cancellationToken)
    {
        // The precursor is looked up among the deliveries of the mandate, which the key lookup does not load.
        var mandate = await context.Mandates
            .AsNoTracking()
            .Include(m => m.Deliveries)
            .SingleAsync(m => m.Id == mandateId, cancellationToken);

        var precursor = mandate.Deliveries.SingleOrDefault(d => d.Id == fields.PrecursorDeliveryId);
        return DeliveryFieldValidator.Validate(mandate, fields, precursor);
    }

    private SubmissionResponse BuildResponse(ProcessingJob job, Submission submission)
    {
        // Both builders produce the download URL of this resource: visualizations are not part of the machine
        // contract and are dropped right after, so they never need a URL of their own.
        var jobResponse = job.ToResponse(BuildDownloadUrl, BuildDownloadUrl);

        var messages = new List<SubmissionMessage>();
        var downloads = new List<SubmissionDownload>();
        foreach (var step in jobResponse.Steps)
        {
            var severity = ToSeverity(step.State);
            if (step.StatusMessage is not null)
                messages.Add(new SubmissionMessage(step.Id, severity, step.StatusMessage));

            if (step.ConditionMessage is not null)
                messages.Add(new SubmissionMessage(step.Id, severity, step.ConditionMessage));

            downloads.AddRange(step.Downloads.Select(download => new SubmissionDownload(step.Id, download.OriginalFileName, download.Url)));
        }

        if (submission.DeclarationFailure is not null)
        {
            messages.Add(new SubmissionMessage(
                "delivery",
                SubmissionMessageSeverity.Error,
                new Dictionary<string, string> { ["en"] = submission.DeclarationFailure }));
        }

        return new SubmissionResponse(job.Id, DeriveState(job, submission), submission.MandateKey, submission.DeliveryId, messages, downloads);
    }

    private static SubmissionState DeriveState(ProcessingJob job, Submission submission)
    {
        if (submission.DeliveryId is not null)
            return SubmissionState.Delivered;

        // A declaration that failed after a run the pipeline allowed is not the fault of the delivered data,
        // so it is a failure like any other: the client retries rather than corrects. Why it failed is in the
        // messages.
        if (submission.DeclarationFailure is not null)
            return SubmissionState.Failed;

        return job.State switch
        {
            ProcessingState.DeliveryRestriction => SubmissionState.Rejected,
            ProcessingState.Failed or ProcessingState.Cancelled => SubmissionState.Failed,

            // A finished run whose delivery is not written yet stays in processing: reporting it as delivered
            // without a delivery id would be a lie.
            _ => SubmissionState.Processing,
        };
    }

    private static SubmissionMessageSeverity ToSeverity(StepState state) => state switch
    {
        StepState.Error or StepState.Cancelled => SubmissionMessageSeverity.Error,
        StepState.Warning or StepState.DeliveryRestriction => SubmissionMessageSeverity.Warning,
        _ => SubmissionMessageSeverity.Info,
    };

    private async Task<(Mandate? Mandate, DeliveryFields? Fields, IActionResult? Failure)> ResolveMandateAsync(Dictionary<string, string> fields, User user, CancellationToken cancellationToken)
    {
        if (!fields.TryGetValue("mandateKey", out var mandateKey) || string.IsNullOrWhiteSpace(mandateKey))
            return (null, null, BadRequest("The form field <mandateKey> is required and must be sent before the files."));

        // The key is free text from the request: it may be reflected to its sender, but it must not reach the log.
        var mandate = await mandateService.GetMandateByKeyForUser(mandateKey, user);
        if (mandate is null)
        {
            logger.LogInformation("A machine delivery was refused because no accessible mandate carries the given key.");
            return (null, null, NotFound($"No mandate with key <{mandateKey}> found."));
        }

        // Without an upload id this covers everything but the file types, which are checked per file below.
        var deliverability = await mandateService.GetDeliverabilityAsync(mandate, uploadId: null);
        if (deliverability == MandateDeliverability.DeliveryNotAllowed)
            return (null, null, Conflict($"Mandate <{mandateKey}> does not accept deliveries."));

        if (deliverability == MandateDeliverability.PipelineNotConfigured)
            return (null, null, Conflict($"Mandate <{mandateKey}> names no pipeline this installation offers, which is a configuration issue of the installation."));

        var deliveryFields = ReadDeliveryFields(fields);
        if (!ModelState.IsValid)
            return (null, null, ValidationProblem(ModelState));

        var fieldErrors = await ValidateDeliveryFieldsAsync(mandate.Id, deliveryFields, cancellationToken);
        if (fieldErrors.Count > 0)
        {
            foreach (var fieldError in fieldErrors)
            {
                foreach (var message in fieldError.Value)
                {
                    ModelState.AddModelError(fieldError.Key, message);
                }
            }

            return (null, null, ValidationProblem(ModelState));
        }

        return (mandate, deliveryFields, null);
    }

    private DeliveryFields ReadDeliveryFields(Dictionary<string, string> fields)
    {
        bool? partialDelivery = null;
        if (fields.TryGetValue("partialDelivery", out var partialText) && !string.IsNullOrWhiteSpace(partialText))
        {
            if (bool.TryParse(partialText, out var parsed))
                partialDelivery = parsed;
            else
                ModelState.AddModelError("partialDelivery", "Must be true or false.");
        }

        int? precursorDeliveryId = null;
        if (fields.TryGetValue("precursorDeliveryId", out var precursorText) && !string.IsNullOrWhiteSpace(precursorText))
        {
            if (int.TryParse(precursorText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                precursorDeliveryId = parsed;
            else
                ModelState.AddModelError("precursorDeliveryId", "Must be a whole number.");
        }

        fields.TryGetValue("comment", out var comment);
        return new DeliveryFields(partialDelivery, precursorDeliveryId, comment);
    }

    private async Task<IActionResult?> StoreFileAsync(
        MultipartSection section,
        ContentDispositionHeaderValue disposition,
        Mandate mandate,
        DirectUploadStorage storage,
        Guid uploadId,
        List<UploadedFileInfo> storedFiles,
        CancellationToken cancellationToken)
    {
        // Never trust the name for a path: only its last segment becomes the storage key.
        var fileName = Path.GetFileName(HeaderUtilities.RemoveQuotes(disposition.FileName).Value);
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("Every file part must carry a file name.");

        if (storedFiles.Any(file => string.Equals(file.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
            return BadRequest($"The file <{fileName}> is sent more than once.");

        if (storedFiles.Count >= uploadOptions.MaxFilesPerJob)
            return BadRequest($"A delivery takes at most {uploadOptions.MaxFilesPerJob} files.");

        var extension = Path.GetExtension(fileName);
        if (!await mandateService.AcceptsFileExtensionAsync(mandate.Id, extension))
            return BadRequest($"The mandate does not accept files of type <{extension}>.");

        var storageKey = $"uploads/{uploadId}/{fileName}";
        var maxFileSize = (long)uploadOptions.MaxFileSizeMB * 1024 * 1024;

        long written;
        try
        {
            // Read through the wrapper: the copy into the file raises an IOException for a body that ended early
            // and for a disk that cannot take the file, and only the first is the caller's to fix. Whatever is
            // still an IOException after this came from the writing side.
            using var body = new RequestBodyStream(section.Body, leaveOpen: true);
            written = await storage.WriteAsync(storageKey, body, cancellationToken);
        }
        catch (InvalidDataException)
        {
            // The reader cut the part off at its length limit, so the file is over the limit and only partly
            // written. The whole attempt is refused anyway; the partial file goes right away.
            await storage.DeleteAsync(storageKey);
            return StatusCode(StatusCodes.Status413PayloadTooLarge, $"File <{fileName}> exceeds the {uploadOptions.MaxFileSizeMB} MB this installation accepts.");
        }

        if (written > maxFileSize)
        {
            await storage.DeleteAsync(storageKey);
            return StatusCode(StatusCodes.Status413PayloadTooLarge, $"File <{fileName}> exceeds the {uploadOptions.MaxFileSizeMB} MB this installation accepts.");
        }

        storedFiles.Add(new UploadedFileInfo(fileName, storageKey, written, section.ContentType));
        return null;
    }

    /// <summary>
    /// Refuses the attempt while the installation is already holding as much as it accepts. The JSON form
    /// inherits these two limits from the upload it references, which was initiated through
    /// <see cref="UploadController"/>; this form creates its upload itself and has to apply them.
    /// </summary>
    private async Task<IActionResult?> RefuseWhileAtCapacityAsync()
    {
        if (uploadStore.GetActiveUploadCount() >= uploadOptions.MaxActiveJobs)
            return AtCapacity($"This installation is already running the {uploadOptions.MaxActiveJobs} uploads it accepts at once.");

        // A multipart request declares no sizes, so its own length is the estimate. Without one (a chunked
        // request) the attempt is charged the largest one this installation would take.
        var incoming = Request.ContentLength ?? (long)uploadOptions.MaxJobSizeMB * 1024 * 1024;
        var held = await uploadStorage.GetTotalSizeAsync("uploads/");
        if (held + incoming > (long)uploadOptions.MaxGlobalActiveSizeMB * 1024 * 1024)
            return AtCapacity($"This installation is already holding the {uploadOptions.MaxGlobalActiveSizeMB} MB of uploads it accepts at once.");

        return null;
    }

    private ObjectResult AtCapacity(string message)
    {
        // An exhausted installation is not a wrong request: a machine client has to tell "correct this" from
        // "come back later", and only the latter is worth retrying unchanged.
        Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        logger.LogInformation("A machine delivery was refused for want of capacity: {Message}", message);
        return StatusCode(StatusCodes.Status503ServiceUnavailable, message);
    }

    /// <summary>
    /// Reads the next part of the form, and reports a body that ended early as such. The multipart reader raises
    /// that as an <see cref="IOException"/>, which is the same type the storage raises when a write fails, and
    /// the two deserve opposite answers.
    /// </summary>
    private static async Task<MultipartSection?> ReadSectionAsync(MultipartReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadNextSectionAsync(cancellationToken);
        }
        catch (IOException ex)
        {
            throw new RequestBodyException("The request body ended before the form was complete.", ex);
        }
    }

    /// <summary>
    /// Reads one form field, or <see langword="null"/> if it is longer than <see cref="MaxFormFieldBytes"/>.
    /// </summary>
    private static async Task<string?> ReadFieldAsync(MultipartSection section, CancellationToken cancellationToken)
    {
        // One byte over the limit is enough to know it is too long, and nothing beyond it is ever read.
        var buffer = new byte[MaxFormFieldBytes + 1];

        int read;
        try
        {
            read = await section.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new RequestBodyException("The request body ended before the form was complete.", ex);
        }

        return read > MaxFormFieldBytes ? null : Encoding.UTF8.GetString(buffer, 0, read);
    }

    private string? ReadBoundary()
    {
        if (string.IsNullOrEmpty(Request.ContentType))
            return null;

        var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(Request.ContentType).Boundary).Value;
        return string.IsNullOrEmpty(boundary) ? null : boundary;
    }

    private void RaiseBodySizeLimit()
    {
        // The attempt as a whole is bounded by the configured job size; the per file limit is checked while the
        // sections are read. The feature is absent under the TestServer, where those checks still apply.
        var feature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = (long)uploadOptions.MaxJobSizeMB * 1024 * 1024;
    }

    private Uri BuildStatusUrl(Guid id)
    {
        var url = Url.RouteUrl(nameof(GetSubmissionStatus), new { id }, Request.Scheme, Request.Host.Value)
            ?? throw new InvalidOperationException($"Could not generate the status URL for submission <{id}>.");
        return new Uri(url);
    }

    private Uri BuildDownloadUrl(Guid id, string fileName)
    {
        var url = Url.RouteUrl(nameof(GetDownload), new { id, name = fileName }, Request.Scheme, Request.Host.Value)
            ?? throw new InvalidOperationException($"Could not generate the download URL for submission <{id}> file <{fileName}>.");
        return new Uri(url);
    }
}
