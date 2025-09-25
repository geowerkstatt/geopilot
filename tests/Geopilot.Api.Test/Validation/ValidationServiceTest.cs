using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Validation;

[TestClass]
public class ValidationServiceTest
{
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IValidator> validatorMock;
    private Mock<Context> contextMock;
    private ValidationService validationService;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IValidationJobStore> validationJobStoreMock;

    [TestInitialize]
    public void Initialize()
    {
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        validatorMock = new Mock<IValidator>(MockBehavior.Strict);
        contextMock = new Mock<Context>(new DbContextOptions<Context>());
        validationJobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);

        validationService = new ValidationService(
            contextMock.Object,
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            new[] { validatorMock.Object });
    }

    [TestCleanup]
    public void Cleanup()
    {
        fileProviderMock.VerifyAll();
        validatorMock.VerifyAll();
        validationJobStoreMock.VerifyAll();
        mandateServiceMock.VerifyAll();
    }

    [TestMethod]
    public void CreateFileHandleForJob()
    {
        const string originalFileName = "BIZARRESCAN.xtf";
        const string tempFileName = "TEMP.xtf";
        using var expectedFileHandle = new FileHandle(tempFileName, Stream.Null);

        var job = new ValidationJob(Guid.NewGuid(), originalFileName, tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created);
        validationJobStoreMock
            .Setup(x => x.GetJob(job.Id))
            .Returns(job);
        fileProviderMock.Setup(x => x.Initialize(job.Id));
        fileProviderMock.Setup(x => x.CreateFileWithRandomName(".xtf")).Returns(expectedFileHandle);

        var actualFileHandle = validationService.CreateFileHandleForJob(job.Id, originalFileName);

        Assert.AreEqual(expectedFileHandle, actualFileHandle);
    }

    [TestMethod]
    public void CreateFileHandleForJobThrowsForUnknownJob()
    {
        var unknownJobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(x => x.GetJob(unknownJobId))
            .Returns((ValidationJob?)null);

        Assert.ThrowsException<ArgumentException>(() => validationService.CreateFileHandleForJob(unknownJobId, "SomeFile.xtf"));
    }

    [TestMethod]
    public async Task StartJobAsyncWithoutMandate()
    {
        var jobId = Guid.NewGuid();
        var tempFileName = "file.xtf";
        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);

        var supportedValidatorMock1 = new Mock<IValidator>(MockBehavior.Strict);
        var supportedValidatorMock2 = new Mock<IValidator>(MockBehavior.Strict);
        var unsupportedValidator = new Mock<IValidator>(MockBehavior.Strict);

        supportedValidatorMock1.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".xtf" });
        supportedValidatorMock2.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".csv", ".xtf" });
        unsupportedValidator.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".csv" });

        validationService = new ValidationService(
            contextMock.Object,
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            new[] { supportedValidatorMock1.Object, supportedValidatorMock2.Object, unsupportedValidator.Object });

        // Expect StartJob to be called with all supported validators
        validationJobStoreMock
            .Setup(x => x.StartJob(
                jobId,
                It.Is<ICollection<IValidator>>(v =>
                    v.Count == 2 && v.Contains(supportedValidatorMock1.Object) && v.Contains(supportedValidatorMock2.Object)),
                null))
            .Returns(job);

        var result = await validationService.StartJobAsync(jobId, null, null);

        Assert.AreEqual(job, result);
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsforUnknownJob()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await validationService.StartJobAsync(jobId, null, null);
        });
    }
}
