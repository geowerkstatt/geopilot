using GeoCop.Api.FileAccess;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GeoCop.Api.Validation;

[TestClass]
public class ValidationServiceTest
{
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IValidationRunner> validationRunnerMock;
    private Mock<IValidator> validatorMock;
    private Mock<Context> contextMock;
    private ValidationService validationService;

    [TestInitialize]
    public void Initialize()
    {
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        validationRunnerMock = new Mock<IValidationRunner>(MockBehavior.Strict);
        validatorMock = new Mock<IValidator>(MockBehavior.Strict);
        contextMock = new Mock<Context>(new DbContextOptions<Context>());

        validationService = new ValidationService(
            fileProviderMock.Object,
            validationRunnerMock.Object,
            new[] { validatorMock.Object },
            contextMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        fileProviderMock.VerifyAll();
        validationRunnerMock.VerifyAll();
        validatorMock.VerifyAll();
    }

    [TestMethod]
    public void CreateValidationJob()
    {
        const string originalFileName = "BIZARRESCAN.xtf";
        using var fileHandle = new FileHandle("TEMP.xtf", Stream.Null);

        fileProviderMock.Setup(x => x.Initialize(It.IsAny<Guid>()));
        fileProviderMock.Setup(x => x.CreateFileWithRandomName(It.Is<string>(x => x == ".xtf"))).Returns(fileHandle);

        var (validationJob, actualFileHandle) = validationService.CreateValidationJob(originalFileName);

        Assert.AreNotEqual(Guid.Empty, validationJob.Id);
        Assert.AreEqual(originalFileName, validationJob.OriginalFileName);
        Assert.AreEqual(fileHandle.FileName, validationJob.TempFileName);
        Assert.AreEqual(fileHandle, actualFileHandle);
    }
}
