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

        var result = await validationService.StartJobAsync(jobId);

        Assert.AreEqual(job, result);
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsForUnknownJob()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await validationService.StartJobAsync(jobId);
        });
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandateId = 123;
        var tempFileName = "file.xtf";
        var user = new User
        {
            Id = 1,
            AuthIdentifier = "user-123",
            Email = "test@example.com",
            FullName = "Test User",
            IsAdmin = false,
            Organisations = new List<Organisation>(),
            Deliveries = new List<Delivery>()
        };

        var mandate = new Mandate
        {
            Id = mandateId,
            Name = "Test Mandate",
            FileTypes = new[] { ".xtf" },
            Organisations = new List<Organisation>(),
            Deliveries = new List<Delivery>()
        };

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready);
        var startedJob = new ValidationJob(jobId, "original.xtf", tempFileName, mandateId, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing);

        var supportedValidatorMock1 = new Mock<IValidator>(MockBehavior.Strict);
        var supportedValidatorMock2 = new Mock<IValidator>(MockBehavior.Strict);

        supportedValidatorMock1.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".xtf" });
        supportedValidatorMock2.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".csv", ".xtf" });

        validationService = new ValidationService(
            contextMock.Object,
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            new[] { supportedValidatorMock1.Object, supportedValidatorMock2.Object });

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateByUserAndJobAsync(mandateId, user, jobId))
            .ReturnsAsync(mandate);
        validationJobStoreMock
            .Setup(x => x.StartJob(
                jobId,
                It.Is<ICollection<IValidator>>(v =>
                    v.Count == 2 && v.Contains(supportedValidatorMock1.Object) && v.Contains(supportedValidatorMock2.Object)),
                mandateId))
            .Returns(startedJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandateId, user);

        // Assert
        Assert.AreEqual(startedJob, result);
        Assert.AreEqual(mandateId, result.MandateId);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForUnknownJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandateId = 123;
        var user = new User
        {
            Id = 1,
            AuthIdentifier = "user-123",
            Email = "test@example.com",
            FullName = "Test User",
            IsAdmin = false,
            Organisations = new List<Organisation>(),
            Deliveries = new List<Delivery>()
        };

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, user);
        });

        Assert.AreEqual($"Validation job with id <{jobId}> not found. (Parameter 'jobId')", exception.Message);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForInvalidMandate()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandateId = 123;
        var tempFileName = "file.xtf";
        var user = new User
        {
            Id = 1,
            AuthIdentifier = "user-123",
            Email = "test@example.com",
            FullName = "Test User",
            IsAdmin = false,
            Organisations = new List<Organisation>(),
            Deliveries = new List<Delivery>()
        };

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateByUserAndJobAsync(mandateId, user, jobId))
            .ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, user);
        });

        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}.", exception.Message);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateUsesCorrectValidators()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandateId = 123;
        var tempFileName = "file.xtf";
        var user = new User
        {
            Id = 1,
            AuthIdentifier = "user-123",
            Email = "test@example.com",
            FullName = "Test User",
            IsAdmin = false,
            Organisations = new List<Organisation>(),
            Deliveries = new List<Delivery>()
        };

        var mandate = new Mandate
        {
            Id = mandateId,
            Name = "Test Mandate",
            FileTypes = new[] { ".xtf" },
            Organisations = new List<Organisation>(),
            Deliveries = new List<Delivery>()
        };

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready);
        var startedJob = new ValidationJob(jobId, "original.xtf", tempFileName, mandateId, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing);

        var mandateSpecificValidatorMock = new Mock<IValidator>(MockBehavior.Strict);
        var unsupportedValidatorMock = new Mock<IValidator>(MockBehavior.Strict);

        mandateSpecificValidatorMock.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".xtf" });
        unsupportedValidatorMock.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync(new List<string> { ".csv" });

        validationService = new ValidationService(
            contextMock.Object,
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            new[] { mandateSpecificValidatorMock.Object, unsupportedValidatorMock.Object });

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateByUserAndJobAsync(mandateId, user, jobId))
            .ReturnsAsync(mandate);
        validationJobStoreMock
            .Setup(x => x.StartJob(
                jobId,
                It.Is<ICollection<IValidator>>(v =>
                    v.Count == 1 && v.Contains(mandateSpecificValidatorMock.Object)),
                mandateId))
            .Returns(startedJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandateId, user);

        // Assert
        Assert.AreEqual(startedJob, result);
        // Verify only the .xtf supporting validator was used, not the .csv one
        mandateSpecificValidatorMock.Verify(x => x.GetSupportedFileExtensionsAsync(), Times.Once);
        unsupportedValidatorMock.Verify(x => x.GetSupportedFileExtensionsAsync(), Times.Once);
    }
}
