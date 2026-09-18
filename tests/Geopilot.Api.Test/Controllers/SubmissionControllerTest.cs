using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Geopilot.Api.Controllers;

/// <summary>
/// The machine delivery resource refuses everything it can refuse in the very first call, before a job exists.
/// </summary>
[TestClass]
public class SubmissionControllerTest
{
    private const string MandateKey = "GRUMPYFALCON";

    private Mock<IMandateService> mandateServiceMock;
    private Mock<IProcessingService> processingServiceMock;
    private Mock<ISubmissionStore> submissionStoreMock;
    private Mock<IUploadOrchestrationService> orchestrationMock;
    private Context context;
    private User user;
    private Mandate mandate;
    private Guid uploadId;
    private string uploadRoot;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        processingServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        submissionStoreMock = new Mock<ISubmissionStore>();
        uploadId = Guid.NewGuid();
        uploadRoot = Path.Combine(Path.GetTempPath(), $"geopilot-submission-{Guid.NewGuid()}");

        (user, mandate) = context.AddMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(SubmissionControllerTest)),
            Key = MandateKey,
            AllowDelivery = true,
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        });
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
        if (Directory.Exists(uploadRoot))
            Directory.Delete(uploadRoot, recursive: true);
    }

    [TestMethod]
    public async Task RefusesTheReferencingFormWhereTheFilesBelongInTheRequest()
    {
        var controller = CreateController(UploadBackend.Direct);

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result, "An installation that takes the files with the request must say so instead of accepting a reference.");
    }

    [TestMethod]
    public async Task RefusesAnUnknownMandateKey()
    {
        var controller = CreateController();
        SetupMandateLookup(found: false);

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        processingServiceMock.Verify(p => p.StartJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    [DataRow(MandateDeliverability.DeliveryNotAllowed, typeof(ConflictObjectResult), DisplayName = "Mandate does not accept deliveries")]
    [DataRow(MandateDeliverability.PipelineNotConfigured, typeof(ConflictObjectResult), DisplayName = "Mandate names no known pipeline")]
    [DataRow(MandateDeliverability.FilesNotAccepted, typeof(BadRequestObjectResult), DisplayName = "Mandate rejects the file types")]
    public async Task RefusesAMandateThatCannotTakeTheDelivery(MandateDeliverability deliverability, Type expected)
    {
        var controller = CreateController();
        SetupMandateLookup();
        SetupDeliverability(deliverability);

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        Assert.IsInstanceOfType(result, expected);
        processingServiceMock.Verify(p => p.StartJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task RefusesAnUnknownUpload()
    {
        var controller = CreateController();
        SetupMandateLookup();
        mandateServiceMock
            .Setup(m => m.GetDeliverabilityAsync(mandate, uploadId))
            .ThrowsAsync(new ArgumentException("unknown upload", "uploadId"));

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result, "An upload this installation does not know must not surface as a server error.");
    }

    [TestMethod]
    public async Task RefusesAnUploadWhoseFilesHaveNoExtension()
    {
        var controller = CreateController();
        SetupMandateLookup();
        mandateServiceMock
            .Setup(m => m.GetDeliverabilityAsync(mandate, uploadId))
            .ThrowsAsync(new InvalidOperationException("no file extension"));

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result, "Files without an extension cannot be checked against a mandate; that is a fault of the request, not of the installation.");
    }

    [TestMethod]
    public async Task RefusesDeliveryDetailsTheMandateDoesNotAllow()
    {
        mandate.EvaluateComment = FieldEvaluationType.Required;
        context.SaveChanges();

        var controller = CreateController();
        SetupMandateLookup();
        SetupDeliverability(MandateDeliverability.Deliverable);

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        var problem = Assert.IsInstanceOfType<ObjectResult>(result).Value as ValidationProblemDetails;
        Assert.IsNotNull(problem, "A violated field rule must be reported per field.");
        Assert.IsTrue(problem.Errors.ContainsKey(nameof(SubmissionRequest.Comment)));
        processingServiceMock.Verify(p => p.StartJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task AcceptsAnAttemptAndRemembersIt()
    {
        var controller = CreateController(withUrlHelper: true);
        SetupMandateLookup();
        SetupDeliverability(MandateDeliverability.Deliverable);

        var jobId = Guid.NewGuid();
        processingServiceMock
            .Setup(p => p.StartJobAsync(uploadId, mandate.Id, It.Is<User>(u => u.Id == user.Id)))
            .ReturnsAsync(new ProcessingJob(jobId, uploadId, mandate.Id, DateTime.UtcNow));

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        var accepted = Assert.IsInstanceOfType<AcceptedResult>(result);
        var response = Assert.IsInstanceOfType<SubmissionResponse>(accepted.Value);
        Assert.AreEqual(jobId, response.Id, "The attempt is addressed by the id of its job.");
        Assert.AreEqual(SubmissionState.Processing, response.State);
        Assert.AreEqual(MandateKey, response.MandateKey);
        submissionStoreMock.Verify(
            s => s.Add(It.Is<Submission>(sub => sub.JobId == jobId && sub.DeclaringUserId == user.Id && sub.MandateKey == MandateKey)),
            Times.Once);
    }

    [TestMethod]
    public async Task RefusesTheInlineFormWhereTheFilesBelongInTheStorage()
    {
        var controller = CreateController();
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result, "An installation whose uploads live outside the API must not take the files from the request.");
    }

    [TestMethod]
    public async Task RefusesAnUnknownMandateKeyBeforeReadingTheFiles()
    {
        var controller = CreateController(UploadBackend.Direct);
        mandateServiceMock
            .Setup(m => m.GetMandateByKeyForUser("NOSUCHKEY", It.Is<User>(u => u.Id == user.Id)))
            .ReturnsAsync(default(Mandate?));
        SetMultipartBody(controller, "NOSUCHKEY", [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        Assert.IsFalse(
            Directory.Exists(Path.Combine(uploadRoot, "uploads")),
            "A refused attempt must not leave any file content behind, because the fields are checked before the files are read.");
    }

    [TestMethod]
    public async Task AcceptsFilesSentWithTheRequest()
    {
        var controller = CreateController(UploadBackend.Direct, withUrlHelper: true);
        SetupMandateLookup();
        mandateServiceMock.Setup(m => m.GetDeliverabilityAsync(mandate, null)).ReturnsAsync(MandateDeliverability.Deliverable);
        mandateServiceMock.Setup(m => m.AcceptsFileExtensionAsync(mandate.Id, ".xtf")).ReturnsAsync(true);

        var jobId = Guid.NewGuid();
        processingServiceMock
            .Setup(p => p.StartJobAsync(It.IsAny<Guid>(), mandate.Id, It.Is<User>(u => u.Id == user.Id)))
            .ReturnsAsync((Guid upload, int mandateId, User declaring) => new ProcessingJob(jobId, upload, mandateId, DateTime.UtcNow));

        SetMultipartBody(controller, MandateKey, [("data.xtf", "transfer content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        var accepted = Assert.IsInstanceOfType<AcceptedResult>(result);
        var response = Assert.IsInstanceOfType<SubmissionResponse>(accepted.Value);
        Assert.AreEqual(jobId, response.Id);
        Assert.AreEqual(MandateKey, response.MandateKey);

        var stored = Directory.GetFiles(uploadRoot, "data.xtf", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(stored, "The file sent with the request must be stored under the upload of the attempt.");
        Assert.AreEqual("transfer content", await File.ReadAllTextAsync(stored));
        submissionStoreMock.Verify(s => s.Add(It.Is<Submission>(sub => sub.JobId == jobId)), Times.Once);
    }

    [TestMethod]
    public async Task RefusesTheAttemptWhileTheInstallationRunsAllTheUploadsItTakes()
    {
        var uploadStore = new UploadStore();
        uploadStore.CreateUpload(Guid.NewGuid(), ImmutableList<UploadedFileInfo>.Empty);
        var controller = CreateController(UploadBackend.Direct, uploadStore: uploadStore, maxActiveJobs: 1);
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        var refused = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, refused.StatusCode, "An exhausted installation is not a wrong request: the caller retries it unchanged.");
        Assert.AreEqual("60", controller.Response.Headers.RetryAfter.ToString(), "A caller told to come back later needs to know when.");
    }

    [TestMethod]
    public async Task RefusesTheAttemptWhileTheInstallationHoldsAllTheUploadsItTakes()
    {
        var controller = CreateController(UploadBackend.Direct, maxGlobalActiveSizeMB: 0);
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        var refused = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, refused.StatusCode);
    }

    [TestMethod]
    public async Task RefusesAFormFieldLongerThanADeliveryDetail()
    {
        var controller = CreateController(UploadBackend.Direct);
        SetMultipartBody(controller, new string('x', 9 * 1024), [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result, "A form field must not be read into memory without a bound of its own.");
    }

    [TestMethod]
    public async Task RefusesTheSameFileTwice()
    {
        var controller = CreateController(UploadBackend.Direct);
        SetupDirectMandate();
        SetMultipartBody(controller, MandateKey, [("data.xtf", "first"), ("data.xtf", "second")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result, "Two parts of the same name would overwrite each other in the storage.");
    }

    [TestMethod]
    public async Task RefusesMoreFilesThanTheInstallationTakes()
    {
        var controller = CreateController(UploadBackend.Direct, maxFilesPerJob: 1);
        SetupDirectMandate();
        SetMultipartBody(controller, MandateKey, [("first.xtf", "content"), ("second.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task RefusesAFileLargerThanTheInstallationAccepts()
    {
        var controller = CreateController(UploadBackend.Direct, maxFileSizeMB: 0);
        SetupDirectMandate();
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        var refused = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, refused.StatusCode);
        Assert.IsEmpty(
            Directory.GetFiles(uploadRoot, "data.xtf", SearchOption.AllDirectories),
            "The file that was too large must not stay behind.");
    }

    [TestMethod]
    public async Task RefusesTheDeliveryDetailsBeforeReadingTheFiles()
    {
        mandate.EvaluateComment = FieldEvaluationType.Required;
        context.SaveChanges();

        var controller = CreateController(UploadBackend.Direct);
        SetupDirectMandate();
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        var problem = Assert.IsInstanceOfType<ObjectResult>(result).Value as ValidationProblemDetails;
        Assert.IsNotNull(problem, "A violated field rule must be reported per field here too.");
        Assert.IsEmpty(
            Directory.GetFiles(uploadRoot, "*", SearchOption.AllDirectories),
            "The fields are checked before the files, so nothing may have been written.");
    }

    [TestMethod]
    public async Task HidesAnAttemptOfAnotherCaller()
    {
        var controller = CreateController();
        var jobId = Guid.NewGuid();
        submissionStoreMock
            .Setup(s => s.GetSubmission(jobId))
            .Returns(new Submission(jobId, MandateKey, user.Id + 1, new DeliveryFields(null, null, null)));

        var result = await controller.GetSubmissionStatus(jobId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result, "An attempt of another caller must be indistinguishable from one that does not exist.");
    }

    [TestMethod]
    public async Task HidesTheDownloadsOfAnAttemptOfAnotherCaller()
    {
        var downloadStoreMock = new Mock<IDownloadFileStore>(MockBehavior.Strict);
        var controller = CreateController(downloadFileStore: downloadStoreMock.Object);
        var jobId = Guid.NewGuid();
        submissionStoreMock
            .Setup(s => s.GetSubmission(jobId))
            .Returns(new Submission(jobId, MandateKey, user.Id + 1, new DeliveryFields(null, null, null)));

        var result = await controller.GetDownload(jobId, "validation_errorLog.log");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        downloadStoreMock.Verify(s => s.Exists(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never, "A foreign attempt must be refused before its files are even looked for.");
    }

    [TestMethod]
    public async Task RefusesAFormFieldThatArrivesAfterAFile()
    {
        var controller = CreateController(UploadBackend.Direct);
        SetupDirectMandate();
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")], fieldAfterTheFiles: ("comment", "Nachlieferung"));

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(
            result,
            "The fields are checked before the first file, so a later one would be dropped without the caller noticing.");
        processingServiceMock.Verify(p => p.StartJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task RefusesABodyThatEndsBeforeItSaidItWould()
    {
        var controller = CreateController(UploadBackend.Direct);
        SetupDirectMandate();
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        // A client whose multipart serializer is off sends fewer bytes than its Content-Length declares. The
        // body then simply ends inside a part, and the multipart reader raises the fault itself while looking
        // for a boundary that never comes.
        var request = controller.ControllerContext.HttpContext.Request;
        var full = ((MemoryStream)request.Body).ToArray();
        request.Body = new MemoryStream(full, 0, full.Length - 10);

        var result = await controller.CreateWithFiles(CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(
            result,
            "What the caller sent wrong must not be reported as a server error; a server error is for what the installation can fix.");
    }

    [TestMethod]
    public async Task ReleasesTheUploadWhenTheJobCannotBeStarted()
    {
        var store = new UploadStore();
        var controller = CreateController(UploadBackend.Direct, uploadStore: store);
        SetupDirectMandate();
        processingServiceMock
            .Setup(p => p.StartJobAsync(It.IsAny<Guid>(), mandate.Id, It.Is<User>(u => u.Id == user.Id)))
            .ThrowsAsync(new InvalidOperationException("The pipeline could not be built."));
        SetMultipartBody(controller, MandateKey, [("data.xtf", "content")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateWithFiles(CancellationToken.None));

        Assert.AreEqual(
            0,
            store.GetActiveUploadCount(),
            "An upload session of an attempt that never started would count against MaxActiveJobs until the age based sweep runs.");
        Assert.IsEmpty(Directory.GetFiles(uploadRoot, "*", SearchOption.AllDirectories), "The files of a failed attempt belong to nobody.");
    }

    [TestMethod]
    [DataRow(ProcessingState.Running, null, null, SubmissionState.Processing, DisplayName = "Still running")]
    [DataRow(ProcessingState.Success, null, null, SubmissionState.Processing, DisplayName = "Run finished, delivery not written yet")]
    [DataRow(ProcessingState.Success, 42, null, SubmissionState.Delivered, DisplayName = "Delivered")]
    [DataRow(ProcessingState.Warning, 42, null, SubmissionState.Delivered, DisplayName = "Delivered despite warnings")]
    [DataRow(ProcessingState.DeliveryRestriction, null, null, SubmissionState.Rejected, DisplayName = "The data did not pass")]
    [DataRow(ProcessingState.Failed, null, null, SubmissionState.Failed, DisplayName = "Run failed")]
    [DataRow(ProcessingState.Cancelled, null, null, SubmissionState.Failed, DisplayName = "Run cancelled")]
    [DataRow(ProcessingState.Success, null, "the database went away", SubmissionState.Failed, DisplayName = "Declaration failed after a run that passed")]
    public async Task ReportsWhereTheAttemptStands(ProcessingState jobState, int? deliveryId, string? declarationFailure, SubmissionState expected)
    {
        var controller = CreateController();
        var jobId = SetupAttempt(deliveryId, declarationFailure, jobState);

        var result = await controller.GetSubmissionStatus(jobId);

        var response = Assert.IsInstanceOfType<SubmissionResponse>(Assert.IsInstanceOfType<OkObjectResult>(result).Value);
        Assert.AreEqual(expected, response.State);
        Assert.AreEqual(deliveryId, response.DeliveryId, "The delivery is the durable handle and must be reported as soon as it exists.");
        Assert.AreEqual(MandateKey, response.MandateKey);
    }

    [TestMethod]
    public async Task SaysWhyADeclarationFailed()
    {
        var controller = CreateController();
        var jobId = SetupAttempt(deliveryId: null, declarationFailure: "the database went away", ProcessingState.Success);

        var result = await controller.GetSubmissionStatus(jobId);

        var response = Assert.IsInstanceOfType<SubmissionResponse>(Assert.IsInstanceOfType<OkObjectResult>(result).Value);
        var message = response.Messages.SingleOrDefault(m => m.Step == "delivery");
        Assert.IsNotNull(message, "A failed declaration is the one thing no pipeline step reports, so the attempt has to.");
        Assert.AreEqual(SubmissionMessageSeverity.Error, message.Severity);
        Assert.AreEqual("the database went away", message.Text["en"]);
    }

    [TestMethod]
    [DataRow(StepState.Error, SubmissionMessageSeverity.Error, DisplayName = "Error")]
    [DataRow(StepState.Cancelled, SubmissionMessageSeverity.Error, DisplayName = "Cancelled")]
    [DataRow(StepState.Warning, SubmissionMessageSeverity.Warning, DisplayName = "Warning")]
    [DataRow(StepState.DeliveryRestriction, SubmissionMessageSeverity.Warning, DisplayName = "Delivery restricted")]
    [DataRow(StepState.Success, SubmissionMessageSeverity.Info, DisplayName = "Success")]
    public async Task ReportsWhatAStepSaidWithItsOwnSeverity(StepState stepState, SubmissionMessageSeverity expected)
    {
        var controller = CreateController(withUrlHelper: true);
        var jobId = SetupAttempt(deliveryId: null, declarationFailure: null, ProcessingState.Running, BuildStep(stepState));

        var result = await controller.GetSubmissionStatus(jobId);

        var response = Assert.IsInstanceOfType<SubmissionResponse>(Assert.IsInstanceOfType<OkObjectResult>(result).Value);
        var messages = response.Messages.Where(m => m.Step == "validation").ToList();
        Assert.HasCount(2, messages, "A step reports its own status message and the condition that put it into this state.");
        Assert.IsTrue(messages.All(m => m.Severity == expected));

        var download = response.Downloads.Single();
        Assert.AreEqual("validation", download.Step);
        Assert.AreEqual("errorLog.log", download.Name, "The name is the one the step gave the file, not the one it is stored under.");
    }

    [TestMethod]
    public async Task ServesADownloadUnderTheNameTheStepGaveIt()
    {
        var downloadStoreMock = new Mock<IDownloadFileStore>();
        downloadStoreMock.Setup(s => s.Exists(It.IsAny<Guid>(), "validation_errorLog.log")).Returns(true);
        downloadStoreMock.Setup(s => s.OpenFile(It.IsAny<Guid>(), "validation_errorLog.log")).Returns(new MemoryStream(Encoding.UTF8.GetBytes("log")));

        var controller = CreateController(downloadFileStore: downloadStoreMock.Object);
        var jobId = SetupAttempt(deliveryId: null, declarationFailure: null, ProcessingState.Success, BuildStep(StepState.Error));

        var result = await controller.GetDownload(jobId, "validation_errorLog.log");

        var file = Assert.IsInstanceOfType<FileStreamResult>(result);
        Assert.AreEqual(
            "errorLog.log",
            file.FileDownloadName,
            "The storage name keeps two steps from colliding; what reaches the caller is the name the step gave the file.");
    }

    private static Mock<IPipelineStep> BuildStep(StepState state)
    {
        var step = new Mock<IPipelineStep>();
        step.SetupGet(s => s.Id).Returns("validation");
        step.SetupGet(s => s.DisplayName).Returns(TestHelpers.Localized("Validation"));
        step.SetupGet(s => s.State).Returns(state);
        step.SetupGet(s => s.StatusMessage).Returns(TestHelpers.Localized("2 errors"));
        step.SetupGet(s => s.ConditionMessage).Returns(TestHelpers.Localized("The validation was not successful."));
        step.SetupGet(s => s.Downloads).Returns(new List<PersistedFile> { new("errorLog.log", "validation_errorLog.log") });
        step.SetupGet(s => s.DeliveryFiles).Returns(Array.Empty<PersistedFile>());
        step.SetupGet(s => s.Visualizations).Returns(Array.Empty<StepVisualization>());
        return step;
    }

    private Guid SetupAttempt(int? deliveryId, string? declarationFailure, ProcessingState jobState, Mock<IPipelineStep>? step = null)
    {
        var jobId = Guid.NewGuid();
        var submission = new Submission(jobId, MandateKey, user.Id, new DeliveryFields(null, null, null))
        {
            DeliveryId = deliveryId,
            DeclarationFailure = declarationFailure,
        };
        submissionStoreMock.Setup(s => s.GetSubmission(jobId)).Returns(submission);

        IPipeline? pipeline = null;
        if (step is not null)
        {
            var pipelineMock = new Mock<IPipeline>();
            pipelineMock.SetupGet(p => p.DisplayName).Returns(TestHelpers.Localized("Pipeline"));
            pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep> { step.Object });
            pipeline = pipelineMock.Object;
        }

        var job = new ProcessingJob(jobId, uploadId, mandate.Id, DateTime.UtcNow) { State = jobState, Pipeline = pipeline };
        processingServiceMock.Setup(p => p.GetJob(jobId)).Returns(job);
        return jobId;
    }

    private void SetupDirectMandate()
    {
        SetupMandateLookup();
        mandateServiceMock.Setup(m => m.GetDeliverabilityAsync(mandate, null)).ReturnsAsync(MandateDeliverability.Deliverable);
        mandateServiceMock.Setup(m => m.AcceptsFileExtensionAsync(mandate.Id, ".xtf")).ReturnsAsync(true);
    }

    private static void SetMultipartBody(
        SubmissionController controller,
        string mandateKey,
        (string FileName, string Content)[] files,
        (string Name, string Value)? fieldAfterTheFiles = null)
    {
        const string boundary = "geopilot-test-boundary";
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"--{boundary}\r\n");
        builder.Append("Content-Disposition: form-data; name=\"mandateKey\"\r\n\r\n");
        builder.Append(CultureInfo.InvariantCulture, $"{mandateKey}\r\n");

        foreach (var (fileName, content) in files)
        {
            builder.Append(CultureInfo.InvariantCulture, $"--{boundary}\r\n");
            builder.Append(CultureInfo.InvariantCulture, $"Content-Disposition: form-data; name=\"files\"; filename=\"{fileName}\"\r\n");
            builder.Append("Content-Type: application/octet-stream\r\n\r\n");
            builder.Append(CultureInfo.InvariantCulture, $"{content}\r\n");
        }

        if (fieldAfterTheFiles is { } late)
        {
            builder.Append(CultureInfo.InvariantCulture, $"--{boundary}\r\n");
            builder.Append(CultureInfo.InvariantCulture, $"Content-Disposition: form-data; name=\"{late.Name}\"\r\n\r\n");
            builder.Append(CultureInfo.InvariantCulture, $"{late.Value}\r\n");
        }

        builder.Append(CultureInfo.InvariantCulture, $"--{boundary}--\r\n");

        var request = controller.ControllerContext.HttpContext.Request;
        request.ContentType = $"multipart/form-data; boundary={boundary}";
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private IUploadStorage CreateUploadStorage(UploadBackend backend)
        => backend == UploadBackend.Direct
            ? new DirectUploadStorage(Options.Create(new UploadDirectOptions { Directory = uploadRoot }), Mock.Of<ILogger<DirectUploadStorage>>())
            : Mock.Of<IUploadStorage>();

    private SubmissionRequest NewRequest() => new() { MandateKey = MandateKey, UploadId = uploadId };

    private void SetupMandateLookup(bool found = true)
        => mandateServiceMock
            .Setup(m => m.GetMandateByKeyForUser(MandateKey, It.Is<User>(u => u.Id == user.Id)))
            .ReturnsAsync(found ? mandate : null);

    private void SetupDeliverability(MandateDeliverability deliverability)
        => mandateServiceMock
            .Setup(m => m.GetDeliverabilityAsync(mandate, uploadId))
            .ReturnsAsync(deliverability);

    private SubmissionController CreateController(
        UploadBackend backend = UploadBackend.Cloud,
        bool withUrlHelper = false,
        IUploadStore? uploadStore = null,
        IDownloadFileStore? downloadFileStore = null,
        int maxFilesPerJob = 12,
        int maxFileSizeMB = 2048,
        int maxActiveJobs = 25,
        int maxGlobalActiveSizeMB = 10240)
    {
        var storage = CreateUploadStorage(backend);
        var store = uploadStore ?? new UploadStore();

        // Stands in for the real release: deletes the files and forgets the session, so a test can tell that a
        // refused attempt left neither behind.
        orchestrationMock = new Mock<IUploadOrchestrationService>();
        orchestrationMock
            .Setup(o => o.ReleaseUploadAsync(It.IsAny<Guid>()))
            .Returns<Guid>(async id =>
            {
                await storage.DeletePrefixAsync($"uploads/{id}/");
                store.RemoveUpload(id);
            });

        var controller = new SubmissionController(
            Mock.Of<ILogger<SubmissionController>>(),
            context,
            mandateServiceMock.Object,
            processingServiceMock.Object,
            submissionStoreMock.Object,
            store,
            storage,
            orchestrationMock.Object,
            downloadFileStore ?? Mock.Of<IDownloadFileStore>(),
            Mock.Of<IContentTypeProvider>(),
            Options.Create(new UploadOptions
            {
                Backend = backend,
                MaxFilesPerJob = maxFilesPerJob,
                MaxFileSizeMB = maxFileSizeMB,
                MaxJobSizeMB = 10240,
                MaxActiveJobs = maxActiveJobs,
                MaxGlobalActiveSizeMB = maxGlobalActiveSizeMB,
            }));

        controller.ControllerContext.HttpContext = new DefaultHttpContext { User = TestHelpers.CreateClaimsPrincipal(user) };

        if (withUrlHelper)
        {
            // Building the response needs absolute URLs, which need both the request and the URL helper.
            controller.ControllerContext.HttpContext.Request.Scheme = "https";
            controller.ControllerContext.HttpContext.Request.Host = new HostString("example.org");
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("https://example.org/api/v1/submission/1");
            controller.Url = urlHelper.Object;
        }

        return controller;
    }
}
