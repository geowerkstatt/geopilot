using Geopilot.Api.Contracts;
using Geopilot.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class UploadControllerTest
{
    private Mock<ILogger<UploadController>> loggerMock;
    private Mock<ICloudOrchestrationService> orchestrationServiceMock;
    private UploadController controller;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<UploadController>>();
        orchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);

        controller = new UploadController(loggerMock.Object, orchestrationServiceMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        orchestrationServiceMock.VerifyAll();
    }

    [TestMethod]
    public async Task InitiateUploadAsyncSuccess()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };
        var expectedResponse = new CloudUploadResponse(
            Guid.NewGuid(),
            [new FileUploadInfo("test.xtf", "https://storage.example.com/presigned-url")],
            DateTime.UtcNow.AddHours(1));

        orchestrationServiceMock
            .Setup(s => s.InitiateUploadAsync(request))
            .ReturnsAsync(expectedResponse);

        var result = await controller.InitiateUploadAsync(request);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
        var createdResult = (CreatedAtActionResult)result;
        Assert.AreEqual(expectedResponse, createdResult.Value);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncReturns400ForArgumentException()
    {
        var request = new CloudUploadRequest { Files = [] };

        orchestrationServiceMock
            .Setup(s => s.InitiateUploadAsync(request))
            .ThrowsAsync(new ArgumentException("At least one file must be specified."));

        var result = await controller.InitiateUploadAsync(request);

        AssertResponseValueType(typeof(ProblemDetails), result);
        var objectResult = (ObjectResult)result;
        Assert.AreEqual(400, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncReturns400ForInvalidOperationException()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        orchestrationServiceMock
            .Setup(s => s.InitiateUploadAsync(request))
            .ThrowsAsync(new InvalidOperationException("Something went wrong."));

        var result = await controller.InitiateUploadAsync(request);

        AssertResponseValueType(typeof(ProblemDetails), result);
        var objectResult = (ObjectResult)result;
        Assert.AreEqual(400, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncReturns500ForUnexpectedException()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        orchestrationServiceMock
            .Setup(s => s.InitiateUploadAsync(request))
            .ThrowsAsync(new InvalidProgramException("Unexpected."));

        var result = await controller.InitiateUploadAsync(request);

        AssertResponseValueType(typeof(ProblemDetails), result);
        var objectResult = (ObjectResult)result;
        Assert.AreEqual(500, objectResult.StatusCode);
    }
}
