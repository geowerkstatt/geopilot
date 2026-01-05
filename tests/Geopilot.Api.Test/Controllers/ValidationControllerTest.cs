using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class ValidationControllerTest
{
    private Context context;
    private Mock<ILogger<ValidationController>> loggerMock;
    private Mock<IValidationService> validationServiceMock;
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IContentTypeProvider> contentTypeProviderMock;
    private Mock<ApiVersion> apiVersionMock;
    private Mock<IFormFile> formFileMock;
    private ValidationController controller;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        loggerMock = new Mock<ILogger<ValidationController>>();
        validationServiceMock = new Mock<IValidationService>(MockBehavior.Strict);
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        contentTypeProviderMock = new Mock<IContentTypeProvider>(MockBehavior.Strict);
        apiVersionMock = new Mock<ApiVersion>(MockBehavior.Strict, 9, 88, null!);
        formFileMock = new Mock<IFormFile>(MockBehavior.Strict);

        controller = new ValidationController(
            loggerMock.Object,
            validationServiceMock.Object,
            fileProviderMock.Object,
            contentTypeProviderMock.Object,
            context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        validationServiceMock.VerifyAll();
        fileProviderMock.VerifyAll();
        contentTypeProviderMock.VerifyAll();
        apiVersionMock.VerifyAll();
        formFileMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task UploadAsync()
    {
        var jobId = Guid.NewGuid();
        const string originalFileName = "BIZARRESCAN.xtf";
        const string tempFileName = "TEMP.xtf";
        formFileMock.SetupGet(x => x.Length).Returns(1234);
        formFileMock.SetupGet(x => x.FileName).Returns(originalFileName);
        formFileMock.Setup(x => x.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));

        var validationJob = new ValidationJob(jobId, originalFileName, tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now);
        using var fileHandle = new FileHandle(tempFileName, Stream.Null);

        validationServiceMock.Setup(x => x.IsFileExtensionSupportedAsync(".xtf")).Returns(Task.FromResult(true));
        validationServiceMock.Setup(x => x.CreateJob()).Returns(validationJob);
        validationServiceMock.Setup(x => x.CreateFileHandleForJob(jobId, originalFileName)).Returns(fileHandle);
        validationServiceMock
            .Setup(x => x.AddFileToJob(jobId, originalFileName, tempFileName))
            .Returns(validationJob);

        var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object) as CreatedAtActionResult;

        Assert.IsInstanceOfType<CreatedAtActionResult>(response);
        Assert.IsInstanceOfType<ValidationJobResponse>(response!.Value);
        Assert.AreEqual(StatusCodes.Status201Created, response.StatusCode);
        Assert.AreEqual("GetStatus", response.ActionName);
        Assert.IsNotNull(response.RouteValues);
        Assert.AreEqual(jobId, response.RouteValues["jobId"]);
        Assert.AreEqual(jobId, ((ValidationJobResponse)response.Value!).JobId);
    }

    [TestMethod]
    public async Task UploadAsyncForNull()
    {
        var response = await controller.UploadAsync(apiVersionMock.Object, null!) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual("Form data <file> cannot be empty.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    [DataRow(".exe")]
    [DataRow("")]
    public async Task UploadInvalidFileExtension(string fileExtension)
    {
        formFileMock.SetupGet(x => x.FileName).Returns("upload" + fileExtension);

        validationServiceMock.Setup(x => x.IsFileExtensionSupportedAsync(fileExtension)).Returns(Task.FromResult(false));

        var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual($"File extension <{fileExtension}> is not supported.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public void GetStatus()
    {
        var jobId = Guid.NewGuid();
        var mandateId = 123;

        validationServiceMock
            .Setup(x => x.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "BIZARRESCAN.xtf", "TEMP.xtf", mandateId, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now));

        var response = controller.GetStatus(jobId) as OkObjectResult;
        var jobResponse = response?.Value as ValidationJobResponse;

        Assert.IsInstanceOfType<OkObjectResult>(response);
        Assert.IsInstanceOfType<ValidationJobResponse>(jobResponse);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(jobId, jobResponse.JobId);
        Assert.AreEqual(mandateId, jobResponse.MandateId);
        Assert.AreEqual(Status.Processing, jobResponse.Status);
    }

    [TestMethod]
    public void GetStatusForInvalid()
    {
        var jobId = Guid.Empty;

        validationServiceMock
            .Setup(x => x.GetJob(Guid.Empty))
            .Returns((ValidationJob?)null);

        var response = controller.GetStatus(jobId) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public void Download()
    {
        var jobId = Guid.NewGuid();
        var fileName = "logfile.log";

        validationServiceMock
            .Setup(x => x.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "original.xtf", "temp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Completed, DateTime.Now));

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.Exists(fileName)).Returns(true);
        fileProviderMock.Setup(x => x.Open(fileName)).Returns(Stream.Null);

        var contentType = "text/plain";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(fileName, out contentType)).Returns(true);

        var response = controller.Download(jobId, fileName) as FileStreamResult;

        Assert.IsInstanceOfType<FileStreamResult>(response);
        Assert.AreEqual("text/plain", response!.ContentType);
        Assert.AreEqual("original_log.log", response.FileDownloadName);
    }

    [TestMethod]
    public void DownloadInvalidJob()
    {
        var jobId = Guid.Empty;

        fileProviderMock.Setup(x => x.Initialize(jobId));
        validationServiceMock
            .Setup(x => x.GetJob(Guid.Empty))
            .Returns((ValidationJob?)null);

        var response = controller.Download(default, "logfile.log") as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public void DownloadMissingLog()
    {
        var jobId = Guid.NewGuid();
        var fileName = "missing-logfile.log";

        validationServiceMock
            .Setup(x => x.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "original.xtf", "temp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Completed, DateTime.Now));

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.Exists(fileName)).Returns(false);

        var response = controller.Download(jobId, fileName) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"No log file <{fileName}> found for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncWithoutMandateSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var startJobRequest = new StartJobRequest { MandateId = null };
        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Processing,
            DateTime.Now);

        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId)).ReturnsAsync(validationJob);

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as OkObjectResult;
        var jobResponse = response?.Value as ValidationJobResponse;

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsInstanceOfType<ValidationJobResponse>(jobResponse);
        Assert.AreEqual(jobId, jobResponse.JobId);
        Assert.AreEqual(Status.Processing, jobResponse.Status);
        Assert.IsNull(jobResponse.MandateId);

        validationServiceMock.Verify(x => x.StartJobAsync(jobId), Times.Once);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Use the helper method to create user and mandate with proper relationships
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = nameof(StartJobAsyncWithMandateSuccess) });
        controller.SetupTestUser(user);

        var startJobRequest = new StartJobRequest { MandateId = mandate.Id };

        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            mandate.Id,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Processing,
            DateTime.Now);

        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob).Verifiable();
        validationServiceMock.Setup(x => x.StartJobAsync(jobId, mandate.Id, user)).ReturnsAsync(validationJob);

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as OkObjectResult;
        var jobResponse = response?.Value as ValidationJobResponse;

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsInstanceOfType<ValidationJobResponse>(jobResponse);
        Assert.AreEqual(jobId, jobResponse.JobId);
        Assert.AreEqual(mandate.Id, jobResponse.MandateId);
        Assert.AreEqual(Status.Processing, jobResponse.Status);

        validationServiceMock.Verify();
        validationServiceMock.Verify(x => x.StartJobAsync(jobId, mandate.Id, user), Times.Once);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns404ForUnknownJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var startJobRequest = new StartJobRequest { MandateId = null };

        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);

        validationServiceMock.Verify(x => x.GetJob(jobId), Times.Once);
        validationServiceMock.Verify(x => x.StartJobAsync(It.IsAny<Guid>()), Times.Never);
        validationServiceMock.Verify(x => x.StartJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns400ForArgumentException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var startJobRequest = new StartJobRequest { MandateId = null };
        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Ready,
            DateTime.Now);

        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId))
            .ThrowsAsync(new ArgumentException("Invalid job state"));

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.AreEqual("Invalid job state", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns400ForInvalidOperationException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = nameof(StartJobAsyncReturns400ForInvalidOperationException) });

        var startJobRequest = new StartJobRequest { MandateId = mandate.Id };
        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Ready,
            DateTime.Now);

        controller.SetupTestUser(user);

        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId, mandate.Id, It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("User not authorized for mandate"));

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.AreEqual("User not authorized for mandate", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns500ForUnexpectedException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var startJobRequest = new StartJobRequest { MandateId = null };
        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Ready,
            DateTime.Now);

#pragma warning disable CA2201 // Do not raise reserved exception types
        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId))
            .ThrowsAsync(new Exception());
#pragma warning restore CA2201 // Do not raise reserved exception types

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.AreEqual("An unexpected error occured.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateReturnsBadRequestWithouUser()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var (user, mandate) = context.AddMandateWithUserOrganisation();

        var startJobRequest = new StartJobRequest { MandateId = mandate.Id };
        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Ready,
            DateTime.Now);

        // Note: Not setting up a user to simulate unauthenticated scenario
        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob);

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.AreEqual("User must be authenticated to start a job with a mandate.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForUnauthorizedUser()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var (user, mandate) = context.AddMandateWithUserOrganisation();
        mandate.Organisations.Clear(); // Remove organisation link to simulate unauthorized user
        context.SaveChanges();

        var startJobRequest = new StartJobRequest { MandateId = mandate.Id };
        var validationJob = new ValidationJob(
            jobId,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Ready,
            DateTime.Now);

        controller.SetupTestUser(user);
        validationServiceMock.Setup(x => x.GetJob(jobId)).Returns(validationJob);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId, mandate.Id, It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("The user is not authorized to start the job with the specified mandate."));

        // Act
        var response = await controller.StartJobAsync(jobId, startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.AreEqual("The user is not authorized to start the job with the specified mandate.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncCorrectlyRoutesToAppropriateServiceMethod()
    {
        // Test that the controller correctly routes to the appropriate service method based on MandateId presence

        // Arrange - Without mandate
        var jobId1 = Guid.NewGuid();
        var startJobRequestWithoutMandate = new StartJobRequest { MandateId = null };
        var validationJobWithoutMandate = new ValidationJob(
            jobId1,
            "test.xtf",
            "temp.xtf",
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Processing,
            DateTime.Now);

        validationServiceMock.Setup(x => x.GetJob(jobId1)).Returns(validationJobWithoutMandate);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId1)).ReturnsAsync(validationJobWithoutMandate);

        // Act - Without mandate
        var responseWithoutMandate = await controller.StartJobAsync(jobId1, startJobRequestWithoutMandate) as OkObjectResult;

        // Assert - Without mandate
        Assert.IsInstanceOfType<OkObjectResult>(responseWithoutMandate);
        validationServiceMock.Verify(x => x.StartJobAsync(jobId1), Times.Once);
        validationServiceMock.Verify(x => x.StartJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);

        // Reset mock behavior for next test
        validationServiceMock.Reset();

        // Arrange - With mandate
        var jobId2 = Guid.NewGuid();
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = nameof(StartJobAsyncCorrectlyRoutesToAppropriateServiceMethod) });

        var startJobRequestWithMandate = new StartJobRequest { MandateId = mandate.Id };

        var validationJobWithMandate = new ValidationJob(
            jobId2,
            "test.xtf",
            "temp.xtf",
            mandate.Id,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Processing,
            DateTime.Now);

        controller.SetupTestUser(user);

        validationServiceMock.Setup(x => x.GetJob(jobId2)).Returns(validationJobWithMandate);
        validationServiceMock.Setup(x => x.StartJobAsync(jobId2, mandate.Id, It.IsAny<User>())).ReturnsAsync(validationJobWithMandate);

        // Act - With mandate
        var responseWithMandate = await controller.StartJobAsync(jobId2, startJobRequestWithMandate) as OkObjectResult;

        // Assert - With mandate
        Assert.IsInstanceOfType<OkObjectResult>(responseWithMandate);
        validationServiceMock.Verify(x => x.StartJobAsync(jobId2, mandate.Id, It.IsAny<User>()), Times.Once);
        validationServiceMock.Verify(x => x.StartJobAsync(It.IsAny<Guid>()), Times.Never);
    }
}
