using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class MachineDeliveryCompletionHandlerTest
{
    private readonly Guid jobId = Guid.NewGuid();

    private Mock<ISubmissionStore> submissionStoreMock;
    private Mock<IDeliveryDeclarationService> declarationServiceMock;
    private MachineDeliveryCompletionHandler handler;

    [TestInitialize]
    public void Initialize()
    {
        submissionStoreMock = new Mock<ISubmissionStore>();
        declarationServiceMock = new Mock<IDeliveryDeclarationService>();
        handler = new MachineDeliveryCompletionHandler(
            Mock.Of<ILogger<MachineDeliveryCompletionHandler>>(),
            submissionStoreMock.Object,
            declarationServiceMock.Object);
    }

    [TestMethod]
    public async Task IgnoresAJobThatIsNoMachineDelivery()
    {
        submissionStoreMock.Setup(s => s.GetSubmission(jobId)).Returns(default(Submission?));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        declarationServiceMock.Verify(
            d => d.DeclareAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "A run started from the web interface declares its delivery itself.");
    }

    [TestMethod]
    public async Task DeclaresTheDeliveryForAnAttempt()
    {
        SetupSubmission();
        SetupDeclaration(new DeliveryDeclarationResult(DeliveryDeclarationStatus.Created, DeliveryId: 42));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        submissionStoreMock.Verify(s => s.MarkDelivered(jobId, 42), Times.Once);
        submissionStoreMock.Verify(s => s.MarkDeclarationFailed(jobId, It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task LeavesAnAttemptUntouchedWhenTheRunDoesNotAllowADelivery()
    {
        SetupSubmission();
        SetupDeclaration(new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotDeliverable, Message: "not deliverable"));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        submissionStoreMock.Verify(s => s.MarkDelivered(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        submissionStoreMock.Verify(
            s => s.MarkDeclarationFailed(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never,
            "Data that did not pass is reported as a rejected attempt, not as a failed declaration.");
    }

    [TestMethod]
    public async Task RecordsWhyTheDeliveryCouldNotBeDeclared()
    {
        SetupSubmission();
        SetupDeclaration(new DeliveryDeclarationResult(DeliveryDeclarationStatus.NoAssets, Message: "No assets found."));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        submissionStoreMock.Verify(s => s.MarkDeclarationFailed(jobId, "No assets found."), Times.Once);
        submissionStoreMock.Verify(s => s.MarkDelivered(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task AdoptsADeliveryTheCallerDeclaredItself()
    {
        SetupSubmission();
        SetupDeclaration(new DeliveryDeclarationResult(DeliveryDeclarationStatus.AlreadyDeclared, DeliveryId: 42, Message: "already delivered"));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        submissionStoreMock.Verify(
            s => s.MarkDelivered(jobId, 42),
            Times.Once,
            "The delivery exists, so reporting the attempt as failed would contradict it.");
        submissionStoreMock.Verify(s => s.MarkDeclarationFailed(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task NamesTheFieldsThatDoNotSatisfyTheMandate()
    {
        SetupSubmission();
        SetupDeclaration(new DeliveryDeclarationResult(
            DeliveryDeclarationStatus.FieldRulesViolated,
            FieldErrors: new Dictionary<string, string[]> { ["comment"] = ["Is required."] }));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        submissionStoreMock.Verify(
            s => s.MarkDeclarationFailed(jobId, It.Is<string>(reason => reason.Contains("comment", StringComparison.Ordinal) && reason.Contains("Is required.", StringComparison.Ordinal))),
            Times.Once,
            "The violated rules are the only place that says which field the caller has to correct.");
    }

    [TestMethod]
    public async Task RecordsAnUnexpectedFailureInsteadOfLettingItEscape()
    {
        SetupSubmission();
        declarationServiceMock
            .Setup(d => d.DeclareAsync(jobId, It.IsAny<DeliveryFields>(), 7, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database went away."));

        await handler.OnJobFinishedAsync(jobId, CancellationToken.None);

        submissionStoreMock.Verify(
            s => s.MarkDeclarationFailed(jobId, It.IsAny<string>()),
            Times.Once,
            "An attempt whose declaration broke must not keep reporting that it is still being processed.");
    }

    private void SetupSubmission()
    {
        var submission = new Submission(jobId, "GRUMPYFALCON", DeclaringUserId: 7, new DeliveryFields(null, null, null));
        submissionStoreMock.Setup(s => s.GetSubmission(jobId)).Returns(submission);
    }

    private void SetupDeclaration(DeliveryDeclarationResult result)
        => declarationServiceMock
            .Setup(d => d.DeclareAsync(jobId, It.IsAny<DeliveryFields>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
}
