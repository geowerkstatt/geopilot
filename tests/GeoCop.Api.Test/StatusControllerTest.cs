using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.Controllers
{
    [TestClass]
    public sealed class StatusControllerTest
    {
        private Mock<ILogger<StatusController>> loggerMock;
        private Mock<IValidatorService> validatorServiceMock;
        private Mock<IFileProvider> fileProviderMock;
        private StatusController controller;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<StatusController>>();
            validatorServiceMock = new Mock<IValidatorService>(MockBehavior.Strict);
            fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);

            controller = new StatusController(
                loggerMock.Object,
                validatorServiceMock.Object,
                fileProviderMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            loggerMock.VerifyAll();
            validatorServiceMock.VerifyAll();

            controller.Dispose();
        }

        [TestMethod]
        public void GetStatus()
        {
            var jobId = new Guid("fadc5142-9043-4fdc-aebf-36c21e13f621");

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            fileProviderMock.Setup(x => x.GetFiles()).Returns(new[] { "SILENTFIRE_LOG.xtf" });
            fileProviderMock.SetupGet(x => x.HomeDirectory).Returns(new DirectoryInfo(TestContext.DeploymentDirectory));

            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(It.Is<Guid>(x => x.Equals(jobId))))
                .Returns((Status.Processing, "WAFFLESPATULA GREENNIGHT"));

            var response = controller.GetStatus(jobId) as OkObjectResult;

            Assert.IsInstanceOfType(response, typeof(OkObjectResult));
            Assert.IsInstanceOfType(response!.Value, typeof(StatusResponse));
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(jobId, ((StatusResponse)response.Value!).JobId);
            Assert.AreEqual(Status.Processing, ((StatusResponse)response.Value).Status);
            Assert.AreEqual("WAFFLESPATULA GREENNIGHT", ((StatusResponse)response.Value).StatusMessage);
        }

        [TestMethod]
        public void GetStatusForInvalid()
        {
            var jobId = new Guid("00000000-0000-0000-0000-000000000000");

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(It.Is<Guid>(x => x.Equals(Guid.Empty))))
                .Returns((default, default!));

            var response = controller.GetStatus(default) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
            Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
        }
    }
}
