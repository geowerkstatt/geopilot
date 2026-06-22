using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class ProcessingControllerTest
{
    private Context context;
    private Mock<ILogger<ProcessingController>> loggerMock;
    private Mock<IProcessingService> validationServiceMock;
    private Mock<IDownloadFileStore> downloadFileStoreMock;
    private Mock<IContentTypeProvider> contentTypeProviderMock;
    private ProcessingController controller;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        loggerMock = new Mock<ILogger<ProcessingController>>();
        validationServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        downloadFileStoreMock = new Mock<IDownloadFileStore>(MockBehavior.Strict);
        contentTypeProviderMock = new Mock<IContentTypeProvider>(MockBehavior.Strict);

        controller = new ProcessingController(
            loggerMock.Object,
            validationServiceMock.Object,
            downloadFileStoreMock.Object,
            contentTypeProviderMock.Object,
            context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        validationServiceMock.VerifyAll();
        downloadFileStoreMock.VerifyAll();
        contentTypeProviderMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public void GetStatus()
    {
        var jobId = Guid.NewGuid();
        var mandateId = 123;

        validationServiceMock
            .Setup(x => x.GetJob(jobId))
            .Returns(new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("BIZARRESCAN.xtf", "TEMP.xtf") }, mandateId, DateTime.Now));

        var response = controller.GetStatus(jobId) as OkObjectResult;
        var jobResponse = response?.Value as ProcessingJobResponse;

        Assert.IsInstanceOfType<OkObjectResult>(response);
        Assert.IsInstanceOfType<ProcessingJobResponse>(jobResponse);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(jobId, jobResponse.JobId);
        Assert.AreEqual(mandateId, jobResponse.MandateId);
        Assert.AreEqual(ProcessingState.Pending, jobResponse.State);
    }

    [TestMethod]
    public void GetStatusForInvalid()
    {
        var jobId = Guid.Empty;

        validationServiceMock
            .Setup(x => x.GetJob(Guid.Empty))
            .Returns((ProcessingJob?)null);

        var response = controller.GetStatus(jobId) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public void DownloadFromDownloadStoreUsesOriginalFileName()
    {
        var jobId = Guid.NewGuid();
        var persistedName = "abc123.log";
        var originalName = "validation.log";

        downloadFileStoreMock.Setup(x => x.Exists(jobId, persistedName)).Returns(true);
        downloadFileStoreMock.Setup(x => x.OpenFile(jobId, persistedName)).Returns(Stream.Null);
        validationServiceMock.Setup(x => x.GetJob(jobId))
            .Returns(BuildJobWithDownload(jobId, new PersistedFile(originalName, persistedName)));

        var contentType = "text/plain";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(persistedName, out contentType)).Returns(true);

        var response = controller.Download(jobId, persistedName) as FileStreamResult;

        Assert.IsInstanceOfType<FileStreamResult>(response);
        Assert.AreEqual("text/plain", response!.ContentType);
        Assert.AreEqual(originalName, response.FileDownloadName);
    }

    [TestMethod]
    public void DownloadFallsBackToPersistedNameWhenJobMissing()
    {
        var jobId = Guid.NewGuid();
        var persistedName = "abc123.log";

        downloadFileStoreMock.Setup(x => x.Exists(jobId, persistedName)).Returns(true);
        downloadFileStoreMock.Setup(x => x.OpenFile(jobId, persistedName)).Returns(Stream.Null);
        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns((ProcessingJob?)null);

        var contentType = "text/plain";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(persistedName, out contentType)).Returns(true);

        var response = controller.Download(jobId, persistedName) as FileStreamResult;

        Assert.IsInstanceOfType<FileStreamResult>(response);
        Assert.AreEqual(persistedName, response!.FileDownloadName);
    }

    [TestMethod]
    public void DownloadMissingLog()
    {
        var jobId = Guid.NewGuid();
        var fileName = "missing-logfile.log";

        downloadFileStoreMock.Setup(x => x.Exists(jobId, fileName)).Returns(false);

        var response = controller.Download(jobId, fileName) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"No file <missing-logfile.log> found for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncSuccess()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = nameof(StartJobAsyncSuccess) });
        controller.SetupTestUser(user);

        var startJobRequest = new StartJobRequest { UploadId = uploadId, MandateId = mandate.Id };

        var processingJob = new ProcessingJob(
            jobId,
            new List<ProcessingJobFile>() { new ProcessingJobFile("test.xtf", "temp.xtf") },
            mandate.Id,
            DateTime.Now);

        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, mandate.Id, user)).ReturnsAsync(processingJob);

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as AcceptedAtActionResult;
        var jobResponse = response?.Value as ProcessingJobResponse;

        // Assert
        Assert.IsInstanceOfType<AcceptedAtActionResult>(response);
        Assert.AreEqual(StatusCodes.Status202Accepted, response!.StatusCode);
        Assert.AreEqual(nameof(ProcessingController.GetStatus), response.ActionName);
        Assert.IsNotNull(response.RouteValues);
        Assert.AreEqual(jobId, response.RouteValues["jobId"]);
        Assert.IsInstanceOfType<ProcessingJobResponse>(jobResponse);
        Assert.AreEqual(jobId, jobResponse!.JobId);
        Assert.AreEqual(mandate.Id, jobResponse.MandateId);
        Assert.AreEqual(ProcessingState.Pending, jobResponse.State);

        validationServiceMock.Verify(x => x.StartJobAsync(uploadId, mandate.Id, user), Times.Once);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "StartJobAsyncWithPublicMandateAsUser")]
    [DataRow(false, DisplayName = "StartJobAsyncWithPublicMandateAsUnauthenticated")]
    public async Task StartJobAsyncWithPublicMandate(bool loggedIn)
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var publicMandate = context.Add(new Mandate
        {
            Name = nameof(StartJobAsyncWithPublicMandate),
            IsPublic = true,
        });

        if (loggedIn)
        {
            var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
            controller.SetupTestUser(user.Entity);
        }

        context.SaveChanges();

        var startJobRequest = new StartJobRequest { UploadId = uploadId, MandateId = publicMandate.Entity.Id };

        var processingJob = new ProcessingJob(
            jobId,
            new List<ProcessingJobFile>() { new ProcessingJobFile("test.xtf", "temp.xtf") },
            publicMandate.Entity.Id,
            DateTime.Now);

        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, publicMandate.Entity.Id, It.IsAny<User?>())).ReturnsAsync(processingJob);

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as AcceptedAtActionResult;

        // Assert
        Assert.IsInstanceOfType<AcceptedAtActionResult>(response);
        Assert.AreEqual(StatusCodes.Status202Accepted, response!.StatusCode);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns404ForUnknownUpload()
    {
        // Arrange
        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = 42 };

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null))
            .ThrowsAsync(new ArgumentException($"Upload with id <{startJobRequest.UploadId}> not found."));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"Upload with id <{startJobRequest.UploadId}> not found.", ((ProblemDetails)response.Value!).Detail);

        validationServiceMock.Verify(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null), Times.Once);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns400ForInvalidOperationException()
    {
        // Arrange
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = nameof(StartJobAsyncReturns400ForInvalidOperationException) });

        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = mandate.Id };

        controller.SetupTestUser(user);

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, mandate.Id, It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("User not authorized for mandate"));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual("User not authorized for mandate", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns500ForUnexpectedException()
    {
        // Arrange
        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = 42 };

#pragma warning disable CA2201 // Do not raise reserved exception types
        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null))
            .ThrowsAsync(new Exception());
#pragma warning restore CA2201 // Do not raise reserved exception types

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, response!.StatusCode);
        Assert.AreEqual("An unexpected error occured.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncWithNonPublicMandateAsUnauthenticated()
    {
        // Arrange
        var nonPublicMandate = context.Add(new Mandate
        {
            Name = nameof(StartJobAsyncWithNonPublicMandateAsUnauthenticated),
            IsPublic = false,
        });

        context.SaveChanges();

        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = nonPublicMandate.Entity.Id };

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, nonPublicMandate.Entity.Id, null))
            .ThrowsAsync(new InvalidOperationException("User not authorized for mandate"));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForUnauthorizedUser()
    {
        // Arrange
        var (user, mandate) = context.AddMandateWithUserOrganisation();
        mandate.Organisations.Clear(); // Remove organisation link to simulate unauthorized user
        context.SaveChanges();

        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = mandate.Id };

        controller.SetupTestUser(user);
        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, mandate.Id, It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("The user is not authorized to start the job with the specified mandate."));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual("The user is not authorized to start the job with the specified mandate.", ((ProblemDetails)response.Value!).Detail);
    }

    private static ProcessingJob BuildJobWithDownload(Guid jobId, PersistedFile persisted)
        => BuildJobWithStepFiles(jobId, downloads: new List<PersistedFile> { persisted }, deliveryFiles: new List<PersistedFile>());

    private static ProcessingJob BuildJobWithStepFiles(Guid jobId, List<PersistedFile> downloads, List<PersistedFile> deliveryFiles)
    {
        var stepMock = new Mock<IPipelineStep>();
        stepMock.SetupGet(s => s.Downloads).Returns(downloads);
        stepMock.SetupGet(s => s.DeliveryFiles).Returns(deliveryFiles);
        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep> { stepMock.Object });
        return new ProcessingJob(jobId, new List<ProcessingJobFile>(), null, DateTime.Now) { Pipeline = pipelineMock.Object };
    }
}
