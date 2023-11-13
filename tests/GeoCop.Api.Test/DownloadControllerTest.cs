using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.Controllers
{
    [TestClass]
    public sealed class DownloadControllerTest
    {
        private Mock<ILogger<DownloadController>> loggerMock;
        private Mock<IValidationService> validationServiceMock;
        private Mock<IFileProvider> fileProviderMock;
        private Mock<IContentTypeProvider> contentTypeProviderMock;
        private DownloadController controller;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<DownloadController>>();
            validationServiceMock = new Mock<IValidationService>(MockBehavior.Strict);
            fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
            contentTypeProviderMock = new Mock<IContentTypeProvider>(MockBehavior.Strict);

            controller = new DownloadController(
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
        }

        [TestMethod]
        public void Download()
        {
            var jobId = new Guid("fadc5142-9043-4fdc-aebf-36c21e13f621");
            var fileName = "logfile.log";

            validationServiceMock
                .Setup(x => x.GetJob(It.Is<Guid>(x => x.Equals(jobId))))
                .Returns(new ValidationJob(jobId, "original.xtf", "temp.xtf"));

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            fileProviderMock.Setup(x => x.Exists(It.Is<string>(x => x == fileName))).Returns(true);
            fileProviderMock.Setup(x => x.Open(It.Is<string>(x => x == fileName))).Returns(Stream.Null);

            var contentType = "text/plain";
            contentTypeProviderMock.Setup(x => x.TryGetContentType(It.Is<string>(x => x == fileName), out contentType)).Returns(true);

            var response = controller.Download(jobId, fileName) as FileStreamResult;

            Assert.IsInstanceOfType(response, typeof(FileStreamResult));
            Assert.AreEqual("text/plain", response!.ContentType);
            Assert.AreEqual("original_log.log", response.FileDownloadName);
        }

        [TestMethod]
        public void DownloadInvalidJob()
        {
            var jobId = new Guid("00000000-0000-0000-0000-000000000000");

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            validationServiceMock
                .Setup(x => x.GetJob(It.Is<Guid>(x => x.Equals(Guid.Empty))))
                .Returns((ValidationJob?)null);

            var response = controller.Download(default, "logfile.log") as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
            Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
        }

        [TestMethod]
        public void DownloadMissingLog()
        {
            var jobId = new Guid("fadc5142-9043-4fdc-aebf-36c21e13f621");
            var fileName = "missing-logfile.log";

            validationServiceMock
                .Setup(x => x.GetJob(It.Is<Guid>(x => x.Equals(jobId))))
                .Returns(new ValidationJob(jobId, "original.xtf", "temp.xtf"));

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            fileProviderMock.Setup(x => x.Exists(It.Is<string>(x => x == fileName))).Returns(false);

            var response = controller.Download(jobId, fileName) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
            Assert.AreEqual($"No log file <{fileName}> found for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
        }
    }
}
