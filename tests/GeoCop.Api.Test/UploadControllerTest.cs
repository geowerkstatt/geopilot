using Asp.Versioning;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.Controllers
{
    [TestClass]
    public sealed class UploadControllerTest
    {
        private readonly string jobId = "28e1adff-765e-4c0b-b667-90458b33e1ca";

        private Mock<ILogger<UploadController>> loggerMock;
        private Mock<IValidator> validatorMock;
        private Mock<PhysicalFileProvider> fileProviderMock;
        private Mock<ApiVersion> apiVersionMock;
        private Mock<IFormFile> formFileMock;
        private Mock<IValidatorService> validatorServiceMock;
        private UploadController controller;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<UploadController>>();
            validatorMock = new Mock<IValidator>(MockBehavior.Strict);
            fileProviderMock = new Mock<PhysicalFileProvider>(MockBehavior.Strict, CreateConfiguration(), "GEOCOP_UPLOADS_DIR");
            validatorServiceMock = new Mock<IValidatorService>(MockBehavior.Strict);
            formFileMock = new Mock<IFormFile>(MockBehavior.Strict);
            apiVersionMock = new Mock<ApiVersion>(MockBehavior.Strict, 9, 88, null!);

            validatorMock.SetupGet(x => x.Id).Returns(new Guid(jobId));

            controller = new UploadController(
                loggerMock.Object,
                validatorMock.Object,
                fileProviderMock.Object,
                validatorServiceMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            loggerMock.VerifyAll();
            validatorMock.VerifyAll();
            fileProviderMock.VerifyAll();
            formFileMock.VerifyAll();
            validatorServiceMock.VerifyAll();
            apiVersionMock.VerifyAll();
        }

        [TestMethod]
        public async Task UploadAsync()
        {
            formFileMock.SetupGet(x => x.Length).Returns(1234);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            formFileMock.Setup(x => x.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            validatorServiceMock.Setup(x => x.EnqueueJobAsync(
                It.Is<Guid>(x => x.Equals(new Guid(jobId))),
                It.IsAny<Func<CancellationToken, Task>>())).Returns(Task.FromResult(0));

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object) as CreatedResult;

            Assert.IsInstanceOfType(response, typeof(CreatedResult));
            Assert.IsInstanceOfType(response!.Value, typeof(UploadResponse));
            Assert.AreEqual(StatusCodes.Status201Created, response.StatusCode);
            Assert.AreEqual($"/api/v9/status/{jobId}", response.Location);
            Assert.AreEqual(jobId, ((UploadResponse)response.Value!).JobId.ToString());
            Assert.AreEqual($"/api/v9/status/{jobId}", ((UploadResponse)response.Value!).StatusUrl!.ToString());
        }

        [TestMethod]
        public async Task UploadAsyncForNull()
        {
            var response = await controller.UploadAsync(apiVersionMock.Object, null!) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
            Assert.AreEqual("Form data <file> cannot be empty.", ((ProblemDetails)response.Value!).Detail);
        }

        private IConfiguration CreateConfiguration() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GEOCOP_UPLOADS_DIR", TestContext.DeploymentDirectory },
            }).Build();
    }
}
