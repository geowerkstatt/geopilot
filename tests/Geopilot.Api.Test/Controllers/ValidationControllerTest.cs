using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
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
            contentTypeProviderMock.Object);
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

        var validationJob = new ValidationJob(jobId, originalFileName, tempFileName, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created);
        using var fileHandle = new FileHandle(tempFileName, Stream.Null);

        validationServiceMock.Setup(x => x.IsFileExtensionSupportedAsync(".xtf")).Returns(Task.FromResult(true));
        validationServiceMock.Setup(x => x.CreateJob()).Returns(validationJob);
        validationServiceMock.Setup(x => x.CreateFileHandleForJob(jobId, originalFileName)).Returns(fileHandle);
        validationServiceMock
            .Setup(x => x.AddFileToJob(jobId, originalFileName, tempFileName))
            .Returns(validationJob);
        validationServiceMock
            .Setup(x => x.StartJobAsync(jobId))
            .Returns(Task.FromResult(validationJob));

        var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object) as CreatedAtActionResult;

        Assert.IsInstanceOfType(response, typeof(CreatedAtActionResult));
        Assert.IsInstanceOfType(response!.Value, typeof(ValidationJobResponse));
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

        Assert.IsInstanceOfType(response, typeof(ObjectResult));
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

        Assert.IsInstanceOfType(response, typeof(ObjectResult));
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual($"File extension <{fileExtension}> is not supported.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public void GetStatus()
    {
        var jobId = Guid.NewGuid();

        validationServiceMock
            .Setup(x => x.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "BIZARRESCAN.xtf", "TEMP.xtf", ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing));

        var response = controller.GetStatus(jobId) as OkObjectResult;
        var jobResponse = response?.Value as ValidationJobResponse;

        Assert.IsInstanceOfType(response, typeof(OkObjectResult));
        Assert.IsInstanceOfType(jobResponse, typeof(ValidationJobResponse));
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(jobId, jobResponse.JobId);
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

        Assert.IsInstanceOfType(response, typeof(ObjectResult));
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
            .Returns(new ValidationJob(jobId, "original.xtf", "temp.xtf", ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Completed));

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.Exists(fileName)).Returns(true);
        fileProviderMock.Setup(x => x.Open(fileName)).Returns(Stream.Null);

        var contentType = "text/plain";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(fileName, out contentType)).Returns(true);

        var response = controller.Download(jobId, fileName) as FileStreamResult;

        Assert.IsInstanceOfType(response, typeof(FileStreamResult));
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

        Assert.IsInstanceOfType(response, typeof(ObjectResult));
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
            .Returns(new ValidationJob(jobId, "original.xtf", "temp.xtf", ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Completed));

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.Exists(fileName)).Returns(false);

        var response = controller.Download(jobId, fileName) as ObjectResult;

        Assert.IsInstanceOfType(response, typeof(ObjectResult));
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"No log file <{fileName}> found for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }
}
