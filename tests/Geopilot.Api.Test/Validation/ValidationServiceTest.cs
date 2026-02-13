using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Validation;

[TestClass]
public class ValidationServiceTest
{
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IValidator> validatorMock;
    private Context context;
    private ValidationService validationService;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IValidationJobStore> validationJobStoreMock;

    [TestInitialize]
    public void Initialize()
    {
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        validatorMock = new Mock<IValidator>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        validationJobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);

        validationService = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            [validatorMock.Object]);
    }

    [TestCleanup]
    public void Cleanup()
    {
        fileProviderMock.VerifyAll();
        validatorMock.VerifyAll();
        validationJobStoreMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public void CreateFileHandleForJob()
    {
        const string originalFileName = "BIZARRESCAN.xtf";
        const string tempFileName = "TEMP.xtf";
        using var expectedFileHandle = new FileHandle(tempFileName, Stream.Null);

        var job = new ValidationJob(Guid.NewGuid(), originalFileName, tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now);
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

        Assert.ThrowsExactly<ArgumentException>(() => validationService.CreateFileHandleForJob(unknownJobId, "SomeFile.xtf"));
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsForUnknownJob()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
        {
            await validationService.StartJobAsync(jobId, 0, null);
        });
    }

    [TestMethod]
    public async Task StartJobAsyncSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandate = new Mandate { Id = 1, Name = nameof(StartJobAsyncSuccess), FileTypes = [".xtf"] };
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncSuccess) };
        var tempFileName = "file.xtf";

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);
        var startedJob = new ValidationJob(jobId, "original.xtf", tempFileName, mandate.Id, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now);

        var supportedValidatorMock1 = new Mock<IValidator>(MockBehavior.Strict);
        var supportedValidatorMock2 = new Mock<IValidator>(MockBehavior.Strict);

        supportedValidatorMock1.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync([".xtf"]);
        supportedValidatorMock2.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync([".csv", ".xtf"]);

        validationService = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            [supportedValidatorMock1.Object, supportedValidatorMock2.Object]);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user))
            .ReturnsAsync(mandate);
        validationJobStoreMock
            .Setup(x => x.StartJob(
                jobId,
                It.Is<ICollection<IValidator>>(v =>
                    v.Count == 2 && v.Contains(supportedValidatorMock1.Object) && v.Contains(supportedValidatorMock2.Object)),
                mandate.Id))
            .Returns(startedJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandate.Id, user);

        // Assert
        Assert.AreEqual(startedJob, result);
        Assert.AreEqual(mandate.Id, result.MandateId);

        // Cleanup for additional mocks
        supportedValidatorMock1.VerifyAll();
        supportedValidatorMock2.VerifyAll();
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForUnsupportedFileType()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tempFileName = "file.xtf";
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncWithMandateThrowsForUnsupportedFileType) };
        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);
        var mandate = new Mandate { Id = mandateId, Name = nameof(StartJobAsyncWithMandateThrowsForUnsupportedFileType), FileTypes = [".csv"] };
        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user))
            .ReturnsAsync(mandate);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, user);
        });
        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}>.", exception.Message);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForInvalidMandate()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tempFileName = "file.xtf";
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncWithMandateThrowsForInvalidMandate) };

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user))
            .ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, user);
        });

        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}>.", exception.Message);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateUsesCorrectValidators()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tempFileName = "file.xtf";
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate
            {
                Name = nameof(StartJobAsyncWithMandateUsesCorrectValidators),
                FileTypes = [".xtf"],
            });

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);
        var startedJob = new ValidationJob(jobId, "original.xtf", tempFileName, mandate.Id, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now);

        var mandateSpecificValidatorMock = new Mock<IValidator>(MockBehavior.Strict);
        var unsupportedValidatorMock = new Mock<IValidator>(MockBehavior.Strict);

        mandateSpecificValidatorMock.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync([".xtf"]);
        unsupportedValidatorMock.Setup(x => x.GetSupportedFileExtensionsAsync())
            .ReturnsAsync([".csv"]);

        validationService = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            [mandateSpecificValidatorMock.Object, unsupportedValidatorMock.Object]);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user))
            .ReturnsAsync(mandate);
        validationJobStoreMock
            .Setup(x => x.StartJob(
                jobId,
                It.Is<ICollection<IValidator>>(v =>
                    v.Count == 1 && v.Contains(mandateSpecificValidatorMock.Object)),
                mandate.Id))
            .Returns(startedJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandate.Id, user);

        // Assert
        Assert.AreEqual(startedJob, result);

        // Verify only the .xtf supporting validator was used, not the .csv one
        mandateSpecificValidatorMock.Verify(x => x.GetSupportedFileExtensionsAsync(), Times.Once);
        unsupportedValidatorMock.Verify(x => x.GetSupportedFileExtensionsAsync(), Times.Once);

        // Cleanup for additional mocks
        mandateSpecificValidatorMock.VerifyAll();
        unsupportedValidatorMock.VerifyAll();
    }
}
