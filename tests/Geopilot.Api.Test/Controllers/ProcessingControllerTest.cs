using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class ProcessingControllerTest
{
    private Context context;
    private Mock<ILogger<ProcessingController>> loggerMock;
    private Mock<IProcessingService> validationServiceMock;
    private Mock<IDownloadFileStore> downloadFileStoreMock;
    private Mock<IVisualizationFileStore> visualizationFileStoreMock;
    private Mock<IContentTypeProvider> contentTypeProviderMock;
    private ProcessingController controller;

    private static readonly TimeSpan JobRetention = TimeSpan.FromHours(1);
    private static readonly LocalizedText PipelineName = TestHelpers.Localized("pipeline");

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        loggerMock = new Mock<ILogger<ProcessingController>>();
        validationServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        downloadFileStoreMock = new Mock<IDownloadFileStore>(MockBehavior.Strict);
        visualizationFileStoreMock = new Mock<IVisualizationFileStore>(MockBehavior.Strict);
        contentTypeProviderMock = new Mock<IContentTypeProvider>(MockBehavior.Strict);

        controller = new ProcessingController(
            loggerMock.Object,
            validationServiceMock.Object,
            downloadFileStoreMock.Object,
            visualizationFileStoreMock.Object,
            contentTypeProviderMock.Object,
            context,
            Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5), JobRetention = JobRetention }));

        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock
            .Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns((UrlRouteContext c) =>
            {
                var values = new RouteValueDictionary(c.Values);
                var segment = c.RouteName == nameof(ProcessingController.Download) ? "files" : "visualizations";
                return $"https://geopilot.test/api/v2/processing/{values["jobId"]}/{segment}/{values["file"]}";
            });
        controller.Url = urlHelperMock.Object;
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        validationServiceMock.VerifyAll();
        downloadFileStoreMock.VerifyAll();
        visualizationFileStoreMock.VerifyAll();
        contentTypeProviderMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task StatusOfAPendingRun()
    {
        var run = await AddRunAsync(heartbeatAgo: TimeSpan.Zero);

        var response = await GetStatusAsync(run.JobId);

        Assert.AreEqual(run.JobId, response.JobId);
        Assert.AreEqual(run.MandateId, response.MandateId);
        Assert.AreEqual(ProcessingState.Pending, response.State);
        Assert.HasCount(1, response.Steps);
        Assert.AreEqual(DtoMapperExtensions.PreflightStepId, response.Steps[0].Id);
        Assert.AreEqual(StepState.Running, response.Steps[0].State);
    }

    [TestMethod]
    public async Task StatusOfARunWaitingForAProcessingSlot()
    {
        var run = await AddRunAsync(heartbeatAgo: TimeSpan.Zero, queued: true, steps: ["validation"], stepState: StepState.Pending);

        var response = await GetStatusAsync(run.JobId);

        Assert.AreEqual(ProcessingState.Running, response.State, "A queued job is running while it waits for a slot.");
        Assert.AreEqual(StepState.Running, response.Steps[0].State, "The preparation shows running until the first step starts.");
        Assert.AreEqual(StepState.Pending, response.Steps[1].State);
    }

    [TestMethod]
    public async Task StatusOfARunningRun()
    {
        var run = await AddRunAsync(heartbeatAgo: TimeSpan.Zero, queued: true, steps: ["validation"]);

        var response = await GetStatusAsync(run.JobId);

        Assert.AreEqual(ProcessingState.Running, response.State);
        Assert.AreEqual(StepState.Success, response.Steps[0].State, "The preparation is over once a step has started.");
        Assert.AreEqual("validation", response.Steps[1].Id);
        Assert.AreEqual(PipelineName, response.PipelineName);
    }

    [TestMethod]
    public async Task StatusOfARunWhosePreflightFailed()
    {
        var run = await AddRunAsync(terminalState: ProcessingState.Failed);

        var response = await GetStatusAsync(run.JobId);

        Assert.AreEqual(ProcessingState.Failed, response.State);
        Assert.AreEqual(StepState.Error, response.Steps[0].State);
        Assert.Contains(run.JobId.ToString(), response.Steps[0].StatusMessage!["en"]);
    }

    [TestMethod]
    public async Task StatusOfAFinishedRun()
    {
        var run = await AddRunAsync(terminalState: ProcessingState.Success, queued: true, steps: ["validation"]);
        var step = run.Steps.Single();
        step.Artifacts.Add(new RecordingArtifact { Kind = ArtifactKind.Download, OriginalFileName = "log.txt", PersistedFileName = "p1.txt" });
        step.Artifacts.Add(new RecordingArtifact { Kind = ArtifactKind.Delivery, OriginalFileName = "data.xtf", PersistedFileName = "p2.xtf" });
        step.Artifacts.Add(new RecordingArtifact { Kind = ArtifactKind.Visualization, OriginalFileName = "map.json", PersistedFileName = "p3.json" });
        await context.SaveChangesAsync();

        var response = await GetStatusAsync(run.JobId);

        Assert.AreEqual(ProcessingState.Success, response.State);
        Assert.AreEqual(StepState.Success, response.Steps[0].State);
        var result = response.Steps[1];
        Assert.AreEqual(StepState.Success, result.State);
        Assert.AreEqual("log.txt", result.Downloads.Single().OriginalFileName);
        Assert.EndsWith($"/{run.JobId}/files/p1.txt", result.Downloads.Single().Url.ToString());
        Assert.AreEqual("p2.xtf", result.Deliveries.Single());
        Assert.AreEqual("map.json", result.Visualizations.Single().OriginalFileName);
        Assert.EndsWith($"/{run.JobId}/visualizations/p3.json", result.Visualizations.Single().Url.ToString());
    }

    [TestMethod]
    public async Task StatusOfADeadRunIsNotFound()
    {
        var run = await AddRunAsync(heartbeatAgo: TimeSpan.FromMinutes(10), queued: true);

        var response = await controller.GetStatus(run.JobId, CancellationToken.None) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, response?.StatusCode);
        Assert.AreEqual($"No job information available for job id <{run.JobId}>", ((ProblemDetails)response!.Value!).Detail);
    }

    [TestMethod]
    public async Task StatusAfterTheRetentionIsNotFound()
    {
        var run = await AddRunAsync(terminalState: ProcessingState.Success, terminalAgo: JobRetention + TimeSpan.FromMinutes(1));

        var response = await controller.GetStatus(run.JobId, CancellationToken.None) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, response?.StatusCode);
    }

    [TestMethod]
    public async Task StatusOfARunFromBeforeTheUpdate()
    {
        var run = await AddRunAsync(terminalState: ProcessingState.Success, withDisplayName: false);

        var response = await GetStatusAsync(run.JobId);

        Assert.AreEqual(LocalizedText.Empty, response.PipelineName);
    }

    [TestMethod]
    public async Task StatusForAnUnknownRunIsNotFound()
    {
        var response = await controller.GetStatus(Guid.NewGuid(), CancellationToken.None) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, response?.StatusCode);
    }

    [TestMethod]
    public async Task DownloadKeepsItsOriginalNameFromTheProtocol()
    {
        var run = await AddRunAsync(terminalState: ProcessingState.Success, queued: true, steps: ["validation"]);
        run.Steps.Single().Artifacts.Add(new RecordingArtifact { Kind = ArtifactKind.Download, OriginalFileName = "validation.log", PersistedFileName = "abc123.log" });
        await context.SaveChangesAsync();
        var persistedName = "abc123.log";

        downloadFileStoreMock.Setup(x => x.Exists(run.JobId, persistedName)).Returns(true);
        downloadFileStoreMock.Setup(x => x.OpenFile(run.JobId, persistedName)).Returns(Stream.Null);

        var contentType = "text/plain";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(persistedName, out contentType)).Returns(true);

        var response = await controller.Download(run.JobId, persistedName, CancellationToken.None) as FileStreamResult;

        Assert.IsInstanceOfType<FileStreamResult>(response);
        Assert.AreEqual("text/plain", response!.ContentType);
        Assert.AreEqual("validation.log", response.FileDownloadName);
    }

    [TestMethod]
    public async Task DownloadFallsBackToPersistedNameWhenTheProtocolKnowsNoRun()
    {
        var jobId = Guid.NewGuid();
        var persistedName = "abc123.log";

        downloadFileStoreMock.Setup(x => x.Exists(jobId, persistedName)).Returns(true);
        downloadFileStoreMock.Setup(x => x.OpenFile(jobId, persistedName)).Returns(Stream.Null);

        var contentType = "text/plain";
        contentTypeProviderMock.Setup(x => x.TryGetContentType(persistedName, out contentType)).Returns(true);

        var response = await controller.Download(jobId, persistedName, CancellationToken.None) as FileStreamResult;

        Assert.IsInstanceOfType<FileStreamResult>(response);
        Assert.AreEqual(persistedName, response!.FileDownloadName);
    }

    [TestMethod]
    public async Task DownloadMissingLog()
    {
        var jobId = Guid.NewGuid();
        var fileName = "missing-logfile.log";

        downloadFileStoreMock.Setup(x => x.Exists(jobId, fileName)).Returns(false);

        var response = await controller.Download(jobId, fileName, CancellationToken.None) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"No file <missing-logfile.log> found for job id <{jobId}>", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncSuccess()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = TestHelpers.Localized(nameof(StartJobAsyncSuccess)) });
        controller.SetupTestUser(user);

        var startJobRequest = new StartJobRequest { UploadId = uploadId, MandateId = mandate.Id };

        var processingJob = new ProcessingJob(jobId, Guid.NewGuid(), mandate.Id, DateTime.Now);

        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, mandate.Id, Supplier.ForUser(user.Id), null)).ReturnsAsync(processingJob);

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as AcceptedAtActionResult;
        var jobResponse = response?.Value as ProcessingJobResponse;

        // Assert
        Assert.IsInstanceOfType<AcceptedAtActionResult>(response);
        Assert.AreEqual(StatusCodes.Status202Accepted, response!.StatusCode);
        Assert.AreEqual(nameof(ProcessingController.GetStatus), response.ActionName);
        Assert.IsNotNull(response.RouteValues);
        Assert.AreEqual(jobId, response.RouteValues["jobId"]);
        Assert.IsInstanceOfType<ProcessingJobResponse>(jobResponse);
        Assert.AreEqual(jobId, jobResponse!.JobId);
        Assert.AreEqual(mandate.Id, jobResponse.MandateId);
        Assert.AreEqual(ProcessingState.Pending, jobResponse.State);

        validationServiceMock.Verify(x => x.StartJobAsync(uploadId, mandate.Id, Supplier.ForUser(user.Id), null), Times.Once);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "StartJobAsyncWithPublicMandateAsUser")]
    [DataRow(false, DisplayName = "StartJobAsyncWithPublicMandateAsUnauthenticated")]
    public async Task StartJobAsyncWithPublicMandate(bool loggedIn)
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var publicMandate = context.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(StartJobAsyncWithPublicMandate)),
            IsPublic = true,
        });

        if (loggedIn)
        {
            var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
            controller.SetupTestUser(user.Entity);
        }

        context.SaveChanges();

        var startJobRequest = new StartJobRequest { UploadId = uploadId, MandateId = publicMandate.Entity.Id };

        var processingJob = new ProcessingJob(jobId, Guid.NewGuid(), publicMandate.Entity.Id, DateTime.Now);

        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, publicMandate.Entity.Id, It.IsAny<Supplier?>(), null)).ReturnsAsync(processingJob);

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as AcceptedAtActionResult;

        // Assert
        Assert.IsInstanceOfType<AcceptedAtActionResult>(response);
        Assert.AreEqual(StatusCodes.Status202Accepted, response!.StatusCode);
    }

    [TestMethod]
    public async Task StartJobAsyncWithATokenNobodyKnowsStartsAnonymously()
    {
        // A valid token whose subject is neither a user nor a registered machine client: a person before the
        // first sign-in through the web created a user, or a machine nobody registered. This endpoint is open
        // to everyone, so such a caller processes like one without a token, and the run records nobody.
        var uploadId = Guid.NewGuid();
        var publicMandate = context.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(StartJobAsyncWithATokenNobodyKnowsStartsAnonymously)),
            IsPublic = true,
        });
        context.SaveChanges();
        controller.SetupTestUser(new User { AuthIdentifier = Guid.NewGuid().ToString(), Email = "nobody@example.com", FullName = "Nobody Known" });

        var startJobRequest = new StartJobRequest { UploadId = uploadId, MandateId = publicMandate.Entity.Id };
        var processingJob = new ProcessingJob(Guid.NewGuid(), Guid.NewGuid(), publicMandate.Entity.Id, DateTime.Now);
        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, publicMandate.Entity.Id, null, null)).ReturnsAsync(processingJob);

        var response = await controller.StartJobAsync(startJobRequest) as AcceptedAtActionResult;

        Assert.IsInstanceOfType<AcceptedAtActionResult>(response, "An unknown subject is no error of the request; the caller is treated as anonymous.");
        validationServiceMock.Verify(x => x.StartJobAsync(uploadId, publicMandate.Entity.Id, null, null), Times.Once);
    }

    [TestMethod]
    [DataRow(MachineClientState.Active, DisplayName = "StartJobAsyncWithAnActiveClientStartsAsThatClient")]
    [DataRow(MachineClientState.Inactive, DisplayName = "StartJobAsyncWithADeactivatedClientStartsAnonymously")]
    public async Task StartJobAsyncResolvesAMachineClientByItsState(MachineClientState state)
    {
        // Deactivating a client is how an administrator revokes it, and this endpoint is open to everyone,
        // so the revocation has to hold here as well: a deactivated client processes like a caller without a
        // token and therefore no longer reaches the mandates of its organisations.
        var uploadId = Guid.NewGuid();
        var mandate = context.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(StartJobAsyncResolvesAMachineClientByItsState)),
            IsPublic = true,
        });
        var client = context.MachineClients.Add(new MachineClient
        {
            AuthIdentifier = Guid.NewGuid().ToString(),
            Name = "SILENTHARBOR",
            State = state,
        });
        context.SaveChanges();
        controller.SetupTestUser(new User { AuthIdentifier = client.Entity.AuthIdentifier });

        var expectedSupplier = state == MachineClientState.Active ? Supplier.ForClient(client.Entity.Id) : null;
        var processingJob = new ProcessingJob(Guid.NewGuid(), Guid.NewGuid(), mandate.Entity.Id, DateTime.Now);
        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, mandate.Entity.Id, expectedSupplier, null)).ReturnsAsync(processingJob);

        var response = await controller.StartJobAsync(new StartJobRequest { UploadId = uploadId, MandateId = mandate.Entity.Id });

        Assert.IsInstanceOfType<AcceptedAtActionResult>(response);
        validationServiceMock.Verify(x => x.StartJobAsync(uploadId, mandate.Entity.Id, expectedSupplier, null), Times.Once);
    }

    [TestMethod]
    [DataRow(UserState.Active, DisplayName = "StartJobAsyncWithAnActiveUserStartsAsThatUser")]
    [DataRow(UserState.Inactive, DisplayName = "StartJobAsyncWithADeactivatedUserStartsAnonymously")]
    public async Task StartJobAsyncResolvesAUserByItsState(UserState state)
    {
        // Like a deactivated client, a deactivated person processes like a caller without a token.
        var uploadId = Guid.NewGuid();
        var mandate = context.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(StartJobAsyncResolvesAUserByItsState)),
            IsPublic = true,
        });
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString(), Email = "deactivated@example.com", FullName = "QUIETLANTERN", State = state });
        context.SaveChanges();
        controller.SetupTestUser(user.Entity);

        var expectedSupplier = state == UserState.Active ? Supplier.ForUser(user.Entity.Id) : null;
        var processingJob = new ProcessingJob(Guid.NewGuid(), Guid.NewGuid(), mandate.Entity.Id, DateTime.Now);
        validationServiceMock.Setup(x => x.StartJobAsync(uploadId, mandate.Entity.Id, expectedSupplier, null)).ReturnsAsync(processingJob);

        var response = await controller.StartJobAsync(new StartJobRequest { UploadId = uploadId, MandateId = mandate.Entity.Id });

        Assert.IsInstanceOfType<AcceptedAtActionResult>(response);
        validationServiceMock.Verify(x => x.StartJobAsync(uploadId, mandate.Entity.Id, expectedSupplier, null), Times.Once);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns404ForUnknownUpload()
    {
        // Arrange
        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = 42 };

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null, null))
            .ThrowsAsync(new ArgumentException($"Upload with id <{startJobRequest.UploadId}> not found."));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status404NotFound, response!.StatusCode);
        Assert.AreEqual($"Upload with id <{startJobRequest.UploadId}> not found.", ((ProblemDetails)response.Value!).Detail);

        validationServiceMock.Verify(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null, null), Times.Once);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns400ForInvalidOperationException()
    {
        // Arrange
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = TestHelpers.Localized(nameof(StartJobAsyncReturns400ForInvalidOperationException)) });

        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = mandate.Id };

        controller.SetupTestUser(user);

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, mandate.Id, It.IsAny<Supplier>(), null))
            .ThrowsAsync(new InvalidOperationException("User not authorized for mandate"));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual("User not authorized for mandate", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns409WhileAJobForTheUploadIsActive()
    {
        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = 42 };
        var exception = new JobAlreadyActiveException(startJobRequest.UploadId);

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null, null))
            .ThrowsAsync(exception);

        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status409Conflict, response!.StatusCode);
        Assert.AreEqual(exception.Message, ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncReturns500ForUnexpectedException()
    {
        // Arrange
        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = 42 };

#pragma warning disable CA2201 // Do not raise reserved exception types
        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, null, null))
            .ThrowsAsync(new Exception());
#pragma warning restore CA2201 // Do not raise reserved exception types

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, response!.StatusCode);
        Assert.AreEqual("An unexpected error occured.", ((ProblemDetails)response.Value!).Detail);
    }

    [TestMethod]
    public async Task StartJobAsyncWithNonPublicMandateAsUnauthenticated()
    {
        // Arrange
        var nonPublicMandate = context.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(StartJobAsyncWithNonPublicMandateAsUnauthenticated)),
            IsPublic = false,
        });

        context.SaveChanges();

        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = nonPublicMandate.Entity.Id };

        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, nonPublicMandate.Entity.Id, null, null))
            .ThrowsAsync(new InvalidOperationException("User not authorized for mandate"));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForUnauthorizedUser()
    {
        // Arrange
        var (user, mandate) = context.AddMandateWithUserOrganisation();
        mandate.Organisations.Clear(); // Remove organisation link to simulate unauthorized user
        context.SaveChanges();

        var startJobRequest = new StartJobRequest { UploadId = Guid.NewGuid(), MandateId = mandate.Id };

        controller.SetupTestUser(user);
        validationServiceMock.Setup(x => x.StartJobAsync(startJobRequest.UploadId, mandate.Id, It.IsAny<Supplier>(), null))
            .ThrowsAsync(new InvalidOperationException("The user is not authorized to start the job with the specified mandate."));

        // Act
        var response = await controller.StartJobAsync(startJobRequest) as ObjectResult;

        // Assert
        Assert.IsInstanceOfType<ObjectResult>(response);
        Assert.AreEqual(StatusCodes.Status400BadRequest, response!.StatusCode);
        Assert.AreEqual("The user is not authorized to start the job with the specified mandate.", ((ProblemDetails)response.Value!).Detail);
    }

    private async Task<ProcessingJobResponse> GetStatusAsync(Guid jobId)
    {
        var result = await controller.GetStatus(jobId, CancellationToken.None) as OkObjectResult;
        Assert.IsNotNull(result);
        return (ProcessingJobResponse)result.Value!;
    }

    // Writes a run into the protocol only, the in-memory job store knows nothing of it. Its steps have started, unless
    // they are Pending. Heartbeat and end come from the database clock.
    private async Task<Recording> AddRunAsync(
        TimeSpan? heartbeatAgo = null,
        bool queued = false,
        ProcessingState? terminalState = null,
        TimeSpan? terminalAgo = null,
        bool withDisplayName = true,
        string[]? steps = null,
        StepState? stepState = null)
    {
        var mandate = new Mandate { Name = TestHelpers.Localized(nameof(ProcessingControllerTest)), FileTypes = [".xtf"], PipelineId = "pipe_a" };
        context.Mandates.Add(mandate);
        var run = new Recording
        {
            JobId = Guid.NewGuid(),
            PipelineId = "pipe_a",
            Definition = "{}",
            AppVersion = "test",
            Mandate = mandate,
            UploadId = Guid.NewGuid(),
            UploadStorageLocation = "https://storage.example.com/uploads",
            UploadInitiatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            QueuedAt = queued ? DateTime.UtcNow : null,
            PipelineDisplayName = withDisplayName ? PipelineName : null,
        };
        var order = 0;
        var state = stepState ?? (terminalState is null ? StepState.Running : StepState.Success);
        foreach (var stepId in steps ?? [])
        {
            run.Steps.Add(new RecordingStep
            {
                Order = order++,
                StepId = stepId,
                DisplayName = TestHelpers.Localized(stepId),
                State = state,
                StartedAt = state == StepState.Pending ? null : DateTime.UtcNow,
            });
        }

        context.Recordings.Add(run);
        await context.SaveChangesAsync();

        var heartbeat = heartbeatAgo ?? TimeSpan.Zero;
        var terminalSince = terminalAgo ?? TimeSpan.Zero;
        await context.Recordings
            .Where(r => r.Id == run.Id)
            .ExecuteUpdateAsync(setters =>
            {
                setters.SetProperty(r => r.HeartbeatAt, r => DateTime.UtcNow - heartbeat);
                if (terminalState is not null)
                    setters.SetProperty(r => r.TerminalState, terminalState).SetProperty(r => r.TerminalAt, r => DateTime.UtcNow - terminalSince);
            });
        return run;
    }
}
