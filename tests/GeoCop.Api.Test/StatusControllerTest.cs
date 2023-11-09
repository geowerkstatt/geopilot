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
        private StatusController controller;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<StatusController>>();
            validatorServiceMock = new Mock<IValidatorService>(MockBehavior.Strict);

            controller = new StatusController(
                loggerMock.Object,
                validatorServiceMock.Object);
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

            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(It.Is<Guid>(x => x.Equals(jobId))))
                .Returns(new ValidationJobStatus(jobId, Status.Processing, "WAFFLESPATULA GREENNIGHT"));

            var response = controller.GetStatus(jobId) as OkObjectResult;

            Assert.IsInstanceOfType(response, typeof(OkObjectResult));
            Assert.IsInstanceOfType(response!.Value, typeof(ValidationJobStatus));
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(jobId, ((ValidationJobStatus)response.Value!).JobId);
            Assert.AreEqual(Status.Processing, ((ValidationJobStatus)response.Value).Status);
        }

        [TestMethod]
        public void GetStatusForInvalid()
        {
            var jobId = new Guid("00000000-0000-0000-0000-000000000000");

            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(It.Is<Guid>(x => x.Equals(Guid.Empty))))
                .Returns((ValidationJobStatus?)null);

            var response = controller.GetStatus(default) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
            Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
        }
    }
}
