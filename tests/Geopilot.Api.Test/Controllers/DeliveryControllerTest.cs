using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Reactions;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Geopilot.Api.Controllers;

[TestClass]
public class DeliveryControllerTest
{
    private static readonly DateTime ListWindow = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private Mock<IProcessingService> processingServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private Mock<ILogger<DeliveryController>> loggerMock;
    private Mock<ILogger<DeliveryCreationService>> creationLoggerMock;
    private Mock<IOptions<DeliveryOptions>> deliveryOptionsMock;
    private Mock<IAuthorizationService> authorizationServiceMock;
    private Mock<IRecorder> recorderMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private DeliveryController deliveryController;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<DeliveryController>>();
        creationLoggerMock = new Mock<ILogger<DeliveryCreationService>>();
        processingServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        assetHandlerMock = new Mock<IAssetHandler>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        deliveryOptionsMock = new Mock<IOptions<DeliveryOptions>>();
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true });

        // The creation service is wired up for real, not mocked: these tests cover the delivery rules, which
        // moved into it, and mocking it here would leave those rules untested.
        var creationService = new DeliveryCreationService(creationLoggerMock.Object, context, processingServiceMock.Object, mandateServiceMock.Object, assetHandlerMock.Object, Mock.Of<IPipelineFactory>(), deliveryOptionsMock.Object);
        authorizationServiceMock = new Mock<IAuthorizationService>();
        authorizationServiceMock
            .Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GeopilotPolicies.Admin))
            .ReturnsAsync(AuthorizationResult.Failed());
        recorderMock = new Mock<IRecorder>();
        var reactionStore = new ReactionStore(Options.Create(new DeliveryReactionOptions()), NullLogger<ReactionStore>.Instance);
        pipelineFactoryMock = new Mock<IPipelineFactory>();
        pipelineFactoryMock.SetupGet(factory => factory.ReactionSets).Returns([]);
        deliveryController = new DeliveryController(loggerMock.Object, context, creationService, mandateServiceMock.Object, assetHandlerMock.Object, deliveryOptionsMock.Object, authorizationServiceMock.Object, reactionStore, recorderMock.Object, pipelineFactoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Dispose first: a failing VerifyAll would otherwise leave the test transaction open, and its uncommitted
        // rows block every parallel test that inserts the same unique key until that test times out.
        context.Dispose();
        processingServiceMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        assetHandlerMock.VerifyAll();
        loggerMock.VerifyAll();
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending, DisplayName = "Preflight still running")]
    [DataRow(ProcessingState.Running, DisplayName = "Pipeline finished, delivery files still being staged")]
    [DataRow(ProcessingState.Failed, DisplayName = "Staging the delivery files failed")]
    [DataRow(ProcessingState.Cancelled, DisplayName = "Staging the delivery files was cut short")]
    [DataRow(ProcessingState.DeliveryRestriction)]
    public async Task CreateFailsJobNotCompleted(ProcessingState jobState)
    {
        var mandateId = context.Mandates.First().Id;
        var guid = SetupProcessingJob(mandateId, jobState);
        deliveryController.SetupTestUser(context.Users.First());
        var deliveriesCount = context.Deliveries.Count();

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
        AssertNoDeliveryFailureRecorded();
    }

    [TestMethod]
    public async Task CreateFailsForARunNobodyMayDeliver()
    {
        // A member of the mandate asks to deliver a run that nobody may deliver, such as an anonymous one on a
        // public mandate. Being a member now does not make up for who started it. The refusal comes before the
        // supplier's membership is looked up, so no lookup is set up for it.
        var (user, publicMandate) = context.AddMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateFailsForARunNobodyMayDeliver)),
            IsPublic = true,
            AllowDelivery = true,
        });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(publicMandate.Id, mayDeliver: false);
        var deliveriesCount = context.Deliveries.Count();

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = jobId })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
        AssertNoDeliveryFailureRecorded();
    }

    [TestMethod]
    public async Task CreateFailsJobNotFound()
    {
        var guid = Guid.NewGuid();
        processingServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(default(ProcessingJob?));
        deliveryController.SetupTestUser(context.Users.First());

        var deliveriesCount = context.Deliveries.Count();

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());

        // The job may have expired after its run allowed a delivery; whether it did, only the protocol knows.
        AssertDeliveryFailureRecorded(guid, $"No job information available for job with id <{guid}>");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateFailsUnauthorizedUser(bool publicMandate)
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
        var addedMandate = context.Mandates.Add(new Mandate() { IsPublic = publicMandate });
        context.SaveChanges();
        var guid = SetupProcessingJob(addedMandate.Entity.Id);
        deliveryController.SetupTestUser(user.Entity);

        // The user belongs to no organisation of the mandate, which being public does not make up for.
        SetupMayDeliver(addedMandate.Entity.Id, user.Entity, mayDeliver: false);

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
        AssertDeliveryFailureRecorded(guid, $"Mandate with id <{addedMandate.Entity.Id}> not found.");
    }

    [TestMethod]
    public async Task CreateWithPublicMandateForAMemberOfItsOrganisation()
    {
        var (user, publicMandate) = SetupMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateWithPublicMandateForAMemberOfItsOrganisation)),
            IsPublic = true,
            AllowDelivery = true,
        });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(publicMandate.Id);
        SetupJobPersistence(jobId);

        var request = new DeliveryRequest
        {
            JobId = jobId,
        };

        var result = (await deliveryController.Create(request)) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateSucceedsWhenPipelineCompletedWithWarnings()
    {
        var (user, publicMandate) = SetupMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateSucceedsWhenPipelineCompletedWithWarnings)),
            IsPublic = true,
            AllowDelivery = true,
        });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(publicMandate.Id, ProcessingState.Warning);
        SetupJobPersistence(jobId);

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = jobId })) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateWithMandateThatDoesNotAllowDelivery()
    {
        var (user, publicMandate) = SetupMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateWithMandateThatDoesNotAllowDelivery)),
            IsPublic = true,
            AllowDelivery = false,
        });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(publicMandate.Id);

        var request = new DeliveryRequest
        {
            JobId = jobId,
        };

        var result = (await deliveryController.Create(request)) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateMinimalDelivery()
    {
        var startTime = DateTime.Now;
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateMinimalDelivery)),
                EvaluateComment = FieldEvaluationType.NotEvaluated,
                EvaluatePartial = FieldEvaluationType.NotEvaluated,
                EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
                AllowDelivery = true,
            });
        deliveryController.SetupTestUser(user);
        Guid jobId = SetupProcessingJob(mandate.Id);
        SetupJobPersistence(jobId);

        var request = new DeliveryRequest
        {
            JobId = jobId,
            Comment = null,
            PartialDelivery = null,
            PrecursorDeliveryId = null,
        };

        var result = (await deliveryController.Create(request)) as ObjectResult;

        context.ChangeTracker.Clear();
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);

        var returnedDelivery = result.Value as Delivery;
        Assert.IsNotNull(returnedDelivery);
        Assert.AreEqual(jobId, returnedDelivery.JobId);
        Assert.AreEqual(string.Empty, returnedDelivery.Comment);
        Assert.IsNull(returnedDelivery.Partial);
        Assert.IsNull(returnedDelivery.PrecursorDelivery);

        var dbDelivery = context.Deliveries
            .Include(d => d.Mandate)
            .FirstOrDefault(d => d.Id == returnedDelivery.Id);

        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(DateTimeKind.Utc, dbDelivery.Date.Kind);
        Assert.IsTrue(dbDelivery.Date > startTime.ToUniversalTime() && dbDelivery.Date < DateTime.UtcNow);
        Assert.AreEqual(jobId, dbDelivery.JobId);
        Assert.AreEqual(mandate.Id, dbDelivery.Mandate.Id);
        Assert.AreEqual(string.Empty, dbDelivery.Comment);
        Assert.IsNull(dbDelivery.Partial);
        Assert.IsNull(dbDelivery.PrecursorDelivery);
    }

    [TestMethod]
    public async Task CreateFailsWhenJobWasAlreadyDelivered()
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateFailsWhenJobWasAlreadyDelivered)),
                EvaluateComment = FieldEvaluationType.NotEvaluated,
                EvaluatePartial = FieldEvaluationType.NotEvaluated,
                EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
                AllowDelivery = true,
            });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(mandate.Id);
        SetupJobPersistence(jobId);
        var request = new DeliveryRequest { JobId = jobId };

        var first = (await deliveryController.Create(request)) as ObjectResult;
        Assert.IsNotNull(first);
        Assert.AreEqual(StatusCodes.Status201Created, first.StatusCode);

        var deliveriesCount = context.Deliveries.Count();
        var second = (await deliveryController.Create(request)) as ObjectResult;
        context.ChangeTracker.Clear();

        Assert.IsNotNull(second);
        Assert.AreEqual(StatusCodes.Status409Conflict, second.StatusCode, "A job that was already delivered must not be delivered a second time.");
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count(), "The rejected request must not have created a delivery.");
        AssertNoDeliveryFailureRecorded();
    }

    [TestMethod]
    public async Task TheDatabaseRefusesASecondDeliveryForTheSameJob()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = TestHelpers.Localized(nameof(TheDatabaseRefusesASecondDeliveryForTheSameJob)), AllowDelivery = true });
        var jobId = Guid.NewGuid();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeliveredByUser = user });
        await context.SaveChangesAsync();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeliveredByUser = user });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(),
            "The guard in the service reads and writes in two statements, so only the database keeps two callers that pass it at the same time from recording the assets of one job twice.");
        context.ChangeTracker.Clear();
    }

    [TestMethod]
    public async Task ADeletedDeliveryDoesNotKeepItsJobFromBeingDeliveredAgain()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = TestHelpers.Localized(nameof(ADeletedDeliveryDoesNotKeepItsJobFromBeingDeliveredAgain)), AllowDelivery = true });
        var jobId = Guid.NewGuid();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeliveredByUser = user, Deleted = true });
        await context.SaveChangesAsync();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeliveredByUser = user });
        await context.SaveChangesAsync();

        Assert.HasCount(
            1,
            context.Deliveries.Where(d => d.JobId == jobId).ToList(),
            "A deleted delivery is invisible to the guard in the service, so the index must not see it either.");
    }

    [TestMethod]
    [DataRow(FieldEvaluationType.NotEvaluated, null, typeof(Delivery), "")]
    [DataRow(FieldEvaluationType.NotEvaluated, "", typeof(Delivery), "")]
    [DataRow(FieldEvaluationType.NotEvaluated, "Test", typeof(ValidationProblemDetails), "")]
    [DataRow(FieldEvaluationType.Optional, null, typeof(Delivery), "")]
    [DataRow(FieldEvaluationType.Optional, "", typeof(Delivery), "")]
    [DataRow(FieldEvaluationType.Optional, "Lorem Ipsum", typeof(Delivery), "Lorem Ipsum")]
    [DataRow(FieldEvaluationType.Required, null, typeof(ValidationProblemDetails), null)]
    [DataRow(FieldEvaluationType.Required, "", typeof(ValidationProblemDetails), null)]
    [DataRow(FieldEvaluationType.Required, "Lorem Ipsum", typeof(Delivery), "Lorem Ipsum")]
    public async Task CreateValidatesComment(FieldEvaluationType evaluaton, string comment, Type responseValueType, string dbValue)
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateValidatesComment)),
                EvaluateComment = evaluaton,
                AllowDelivery = true,
            });
        deliveryController.SetupTestUser(user);
        Guid jobId = SetupProcessingJob(mandate.Id);
        if (responseValueType == typeof(Delivery))
        {
            SetupJobPersistence(jobId);
        }

        var request = new DeliveryRequest
        {
            JobId = jobId,
            Comment = comment,
        };

        var result = await deliveryController.Create(request);
        Assert.IsNotNull(result);
        AssertResponseValueType(responseValueType, result);

        if (responseValueType == typeof(Delivery))
        {
            var id = ((result as ObjectResult)?.Value as Delivery)?.Id;
            var dbDelivery = context.Deliveries.Find(id);
            Assert.IsNotNull(dbDelivery);
            Assert.AreEqual(dbValue, dbDelivery.Comment);
        }
    }

    [TestMethod]
    [DataRow(FieldEvaluationType.NotEvaluated, null, typeof(Delivery), null)]
    [DataRow(FieldEvaluationType.NotEvaluated, true, typeof(ValidationProblemDetails), null)]
    [DataRow(FieldEvaluationType.NotEvaluated, false, typeof(ValidationProblemDetails), null)]
    [DataRow(FieldEvaluationType.Required, null, typeof(ValidationProblemDetails), null)]
    [DataRow(FieldEvaluationType.Required, true, typeof(Delivery), true)]
    [DataRow(FieldEvaluationType.Required, false, typeof(Delivery), false)]
    public async Task CreateValidatesPartialDelivery(FieldEvaluationType evaluaton, bool? partialDelivery, Type responseValueType, bool? dbValue)
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateValidatesPartialDelivery)),
                EvaluatePartial = evaluaton,
                AllowDelivery = true,
            });
        deliveryController.SetupTestUser(user);
        Guid jobId = SetupProcessingJob(mandate.Id);
        if (responseValueType == typeof(Delivery))
        {
            SetupJobPersistence(jobId);
        }

        var request = new DeliveryRequest
        {
            JobId = jobId,
            PartialDelivery = partialDelivery,
        };

        var result = await deliveryController.Create(request);
        Assert.IsNotNull(result);
        AssertResponseValueType(responseValueType, result);

        if (responseValueType == typeof(Delivery))
        {
            var id = ((result as ObjectResult)?.Value as Delivery)?.Id;
            var dbDelivery = context.Deliveries.Find(id);
            Assert.IsNotNull(dbDelivery);
            Assert.AreEqual(dbValue, dbDelivery.Partial);
        }
    }

    [TestMethod]
    [DataRow(FieldEvaluationType.NotEvaluated, true, typeof(ValidationProblemDetails))]
    [DataRow(FieldEvaluationType.NotEvaluated, false, typeof(Delivery))]
    [DataRow(FieldEvaluationType.Optional, true, typeof(Delivery))]
    [DataRow(FieldEvaluationType.Optional, false, typeof(Delivery))]
    [DataRow(FieldEvaluationType.Required, true, typeof(Delivery))]
    [DataRow(FieldEvaluationType.Required, false, typeof(ValidationProblemDetails))]
    public async Task CreateValidatesPrecursorDelivery(FieldEvaluationType evaluaton, bool setPrecursor, Type responseValueType)
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateValidatesPrecursorDelivery)),
                EvaluatePrecursorDelivery = evaluaton,
                AllowDelivery = true,
            });
        deliveryController.SetupTestUser(user);
        Guid jobId = SetupProcessingJob(mandate.Id);
        if (responseValueType == typeof(Delivery))
            SetupJobPersistence(jobId);
        var precursorDelivery = new Delivery() { JobId = Guid.NewGuid(), Mandate = mandate, DeliveredByUser = user };
        context.Deliveries.Add(precursorDelivery);
        context.SaveChanges();

        int? precursorId = setPrecursor ? precursorDelivery.Id : null;

        var request = new DeliveryRequest
        {
            JobId = jobId,
            PrecursorDeliveryId = precursorId,
        };

        var response = await deliveryController.Create(request);
        Assert.IsNotNull(response);
        AssertResponseValueType(responseValueType, response);
    }

    [TestMethod]
    public async Task CreateFailsPrecursorFromOtherMandate()
    {
        var deliveriesCount = context.Deliveries.Count();
        var (user, mandate) = SetupMandateWithUserOrganisation(new Mandate { Name = TestHelpers.Localized(nameof(CreateFailsPrecursorFromOtherMandate)), AllowDelivery = true, });
        deliveryController.SetupTestUser(user);
        var guid = SetupProcessingJob(mandate.Id);
        mandate.EvaluatePrecursorDelivery = FieldEvaluationType.Required;
        var otherMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized(nameof(CreateFailsPrecursorFromOtherMandate)), AllowDelivery = true, }).Entity;
        var precursorDelivery = context.Deliveries.Add(new Delivery() { JobId = Guid.NewGuid(), Mandate = otherMandate, DeliveredByUser = user }).Entity;
        context.SaveChanges();

        var request = new DeliveryRequest
        {
            JobId = guid,
            PrecursorDeliveryId = precursorDelivery.Id,
        };

        Assert.AreNotEqual(0, request.PrecursorDeliveryId);

        var result = await deliveryController.Create(request);
        Assert.IsNotNull(result);
        AssertResponseValueType(typeof(ValidationProblemDetails), result);
    }

    [TestMethod]
    public async Task CreateFailsPrecursorNotFound()
    {
        var deliveriesCount = context.Deliveries.Count();
        var (user, mandate) = SetupMandateWithUserOrganisation(new Mandate { Name = TestHelpers.Localized(nameof(CreateFailsPrecursorNotFound)), AllowDelivery = true, });
        deliveryController.SetupTestUser(user);
        var guid = SetupProcessingJob(mandate.Id);
        mandate.EvaluatePrecursorDelivery = FieldEvaluationType.Required;
        var unknownDeliveryId = context.Deliveries.Max(d => d.Id) + 1;

        var request = new DeliveryRequest
        {
            JobId = guid,
            PrecursorDeliveryId = unknownDeliveryId,
        };
        var result = await deliveryController.Create(request);
        Assert.IsNotNull(result);
        AssertResponseValueType(typeof(ValidationProblemDetails), result);
    }

    [TestMethod]
    public async Task CreateFailsWithoutFiles()
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(new Mandate { Name = TestHelpers.Localized(nameof(CreateFailsWithoutFiles)), AllowDelivery = true, });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(mandate.Id);
        var request = new DeliveryRequest
        {
            JobId = jobId,
        };
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset>());

        var result = await deliveryController.Create(request);

        var objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        AssertDeliveryFailureRecorded(jobId, $"No assets found for job with id <{jobId}>.");
    }

    [TestMethod]
    public async Task CreateRecordsThatTheFilesCouldNotBeStored()
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(new Mandate { Name = TestHelpers.Localized(nameof(CreateRecordsThatTheFilesCouldNotBeStored)), AllowDelivery = true });
        deliveryController.SetupTestUser(user);
        var jobId = SetupProcessingJob(mandate.Id);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("The disk is full."));

        var result = await deliveryController.Create(new DeliveryRequest { JobId = jobId });

        var objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        AssertDeliveryFailureRecorded(jobId, $"Error while persisting assets for job with id <{jobId}>.");
    }

    [TestMethod]
    public async Task CreateRecordsAnUnexpectedErrorAndRethrowsIt()
    {
        deliveryController.SetupTestUser(context.Users.First());
        var jobId = Guid.NewGuid();
        var failure = new InvalidOperationException("The job store went away.");
        processingServiceMock.Setup(s => s.GetJob(jobId)).Throws(failure);

        var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => deliveryController.Create(new DeliveryRequest { JobId = jobId }));

        Assert.AreSame(failure, thrown, "The error must reach the error handling of the host as before.");
        AssertDeliveryFailureRecorded(jobId, "The delivery could not be created because of an unexpected error.");
    }

    [TestMethod]
    public async Task CreateRecordsNothingForARequestTheBrowserAborted()
    {
        // Not through SetupMandateWithUserOrganisation: the aborted creation stops before it asks about the membership.
        var (user, mandate) = context.AddMandateWithUserOrganisation(new Mandate { Name = TestHelpers.Localized(nameof(CreateRecordsNothingForARequestTheBrowserAborted)), AllowDelivery = true });
        var httpContextMock = deliveryController.SetupTestUser(user);
        httpContextMock.SetupGet(c => c.RequestAborted).Returns(new CancellationToken(canceled: true));
        var jobId = SetupProcessingJob(mandate.Id);

        await Assert.ThrowsAsync<OperationCanceledException>(() => deliveryController.Create(new DeliveryRequest { JobId = jobId }));

        AssertNoDeliveryFailureRecorded();
    }

    [TestMethod]
    public async Task CreateRecordsNothingWhenTheBrowserAbortsWhileTheFilesAreStored()
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(new Mandate { Name = TestHelpers.Localized(nameof(CreateRecordsNothingWhenTheBrowserAbortsWhileTheFilesAreStored)), AllowDelivery = true });
        var httpContextMock = deliveryController.SetupTestUser(user);
        using var aborted = new CancellationTokenSource();
        httpContextMock.SetupGet(c => c.RequestAborted).Returns(aborted.Token);
        var jobId = SetupProcessingJob(mandate.Id);

        // Storing the files reports the abort as a failure of its own.
        assetHandlerMock
            .Setup(a => a.RecordJobAssetsAsync(jobId, aborted.Token))
            .Callback(() => aborted.Cancel())
            .ThrowsAsync(new OperationCanceledException(aborted.Token));

        var result = await deliveryController.Create(new DeliveryRequest { JobId = jobId });

        var objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        AssertNoDeliveryFailureRecorded();
    }

    private void AssertDeliveryFailureRecorded(Guid jobId, string reason)
        => recorderMock.Verify(r => r.RecordDeliveryFailedAsync(jobId, reason), Times.Once());

    private void AssertNoDeliveryFailureRecorded()
        => recorderMock.Verify(r => r.RecordDeliveryFailedAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never());

    private void SetupJobPersistence(Guid jobId)
    {
        assetHandlerMock
            .Setup(p => p.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Asset> { new Asset(), new Asset() });
        assetHandlerMock.Setup(p => p.PromoteJobAssets(jobId));
    }

    private Guid SetupProcessingJob(int? mandateId = null, ProcessingState jobState = ProcessingState.Success, bool mayDeliver = true)
    {
        var guid = Guid.NewGuid();

        // The pipeline always reports Success, so a refused delivery shows that the gate reads the job state:
        // the job only becomes deliverable once its delivery files are staged.
        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.State).Returns(ProcessingState.Success);
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        pipelineMock.SetupGet(p => p.DisplayName).Returns(LocalizedText.Empty);

        var job = new ProcessingJob(guid, Guid.NewGuid(), mandateId, DateTime.Now)
        {
            Pipeline = pipelineMock.Object,
            State = jobState,
            MayDeliver = mayDeliver,
        };

        processingServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(job);
        return guid;
    }

    [TestMethod]
    public async Task DeleteAsAdmin()
    {
        var admin = context.Users.First(u => u.IsAdmin);
        var user = context.Users.First(u => !u.IsAdmin);

        deliveryController.SetupTestUser(admin);

        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeliveredByUser = user };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        assetHandlerMock.Setup(h => h.DeleteJobAssets(guid));

        var result = await deliveryController.Delete(delivery.Id) as OkResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);

        var dbDelivery = context.DeliveriesWithIncludes
            .IgnoreQueryFilters()
            .FirstOrDefault(d => d.Id == delivery.Id);
        Assert.IsNotNull(dbDelivery);
        Assert.IsTrue(dbDelivery.Deleted);
        Assert.IsTrue(dbDelivery.Assets.All(a => a.Deleted));
    }

    [TestMethod]
    public async Task DeleteEndsTheOpenReactionsAndRecordsHowFarTheyGot()
    {
        var admin = context.Users.First(u => u.IsAdmin);
        deliveryController.SetupTestUser(admin);
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = context.Users.First(u => !u.IsAdmin) };
        context.Deliveries.Add(delivery);
        context.DeliveryReactions.AddRange(NewReaction(delivery, "ablage", ReactionState.Pending), NewReaction(delivery, "meldung", ReactionState.Done));
        context.SaveChanges();
        assetHandlerMock.Setup(h => h.DeleteJobAssets(delivery.JobId));

        Assert.IsInstanceOfType<OkResult>(await deliveryController.Delete(delivery.Id));

        var states = context.DeliveryReactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.DeliveryId == delivery.Id)
            .ToDictionary(r => r.ReactionId, r => r.State);
        Assert.AreEqual(ReactionState.Deleted, states["ablage"]);
        Assert.AreEqual(ReactionState.Done, states["meldung"], "a reaction that already forwarded the delivery stays done.");
        recorderMock.Verify(
            recorder => recorder.RecordDeliveryDeletedAsync(
                delivery.Id,
                It.Is<IReadOnlyList<DeliveryReaction>>(reactions => reactions.Count == 2 && reactions.Any(r => r.ReactionId == "ablage" && r.State == ReactionState.Deleted)),
                admin.Id),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsUploader()
    {
        var user = context.Users.First(u => !u.IsAdmin);

        deliveryController.SetupTestUser(user);

        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeliveredByUser = user };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        assetHandlerMock.Setup(h => h.DeleteJobAssets(guid));

        var result = await deliveryController.Delete(delivery.Id) as OkResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);

        var dbDelivery = context.DeliveriesWithIncludes
            .IgnoreQueryFilters()
            .FirstOrDefault(d => d.Id == delivery.Id);
        Assert.IsNotNull(dbDelivery);
        Assert.IsTrue(dbDelivery.Deleted);
        Assert.IsTrue(dbDelivery.Assets.All(a => a.Deleted));
    }

    [TestMethod]
    [DataRow(ReactionState.Pending, 1, DisplayName = "waiting for its next attempt")]
    [DataRow(ReactionState.Running, 1, DisplayName = "running")]
    [DataRow(ReactionState.Done, 1, DisplayName = "done")]
    [DataRow(ReactionState.Error, 3, DisplayName = "given up")]
    public async Task DeleteAsUploaderFailsOnceAReactionHasStarted(ReactionState state, int attempts)
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = user };
        context.Deliveries.Add(delivery);
        context.DeliveryReactions.Add(NewReaction(delivery, "ablage", state, attempts: attempts));
        context.SaveChanges();

        var result = await deliveryController.Delete(delivery.Id) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status403Forbidden, result?.StatusCode);
        AssertNotDeleted(delivery);
        Assert.AreEqual(state, context.DeliveryReactions.IgnoreQueryFilters().AsNoTracking().Single(r => r.DeliveryId == delivery.Id).State);
    }

    [TestMethod]
    public async Task DeleteAsUploaderEndsAReactionThatStillWaitsForTheWindow()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = user };
        context.Deliveries.Add(delivery);
        context.DeliveryReactions.Add(NewReaction(delivery, "ablage", ReactionState.Pending));
        context.SaveChanges();
        assetHandlerMock.Setup(h => h.DeleteJobAssets(delivery.JobId));

        Assert.IsInstanceOfType<OkResult>(await deliveryController.Delete(delivery.Id));

        Assert.AreEqual(ReactionState.Deleted, context.DeliveryReactions.IgnoreQueryFilters().AsNoTracking().Single(r => r.DeliveryId == delivery.Id).State);
    }

    [TestMethod]
    public async Task DeleteAsAdminFailsDeliveryNotFound()
    {
        var admin = context.Users.First(u => u.IsAdmin);

        deliveryController.SetupTestUser(admin);

        var result = await deliveryController.Delete(context.Deliveries.Max(d => d.Id) + 1) as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);

        assetHandlerMock.Verify(h => h.DeleteJobAssets(It.IsAny<Guid>()), Times.Never());
    }

    [TestMethod]
    public async Task DeleteAsUserFailsIfNotUploader()
    {
        var uploader = context.Users.First();
        var deleter = context.Users.First(u => u.Id != uploader.Id && !u.IsAdmin);

        deliveryController.SetupTestUser(deleter);

        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeliveredByUser = uploader };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        var result = await deliveryController.Delete(delivery.Id) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);

        AssertNotDeleted(delivery);
    }

    [TestMethod]
    [DataRow(false, null, null)]
    [DataRow(true, "10:00:00", null)]
    [DataRow(true, null, "0 * * * *")] // every hour at :00
    public async Task DeleteAsUserFailsIfDisabledByOptions(bool enabled, string? duration, string? interval)
    {
        var uploader = context.Users.First(u => !u.IsAdmin);

        deliveryController.SetupTestUser(uploader);

        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeliveredByUser = uploader, Date = DateTime.UtcNow.AddHours(-12) };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        var options = new DeliveryOptions
        {
            UploaderDeleteEnabled = enabled,
            DeleteDuration = duration == null ? null : TimeSpan.Parse(duration, CultureInfo.InvariantCulture),
            DeleteRestrictInterval = interval,
        };
        deliveryOptionsMock.Setup(o => o.Value).Returns(options);

        var result = Assert.IsInstanceOfType<ObjectResult>(await deliveryController.Delete(delivery.Id));
        Assert.AreEqual(StatusCodes.Status403Forbidden, result.StatusCode);

        AssertNotDeleted(delivery);
    }

    [TestMethod]
    [DataRow(true, true, null, null)]
    [DataRow(true, true, "0:45:00", null)]
    [DataRow(true, true, null, "0 * * * *")] // every hour at :00
    [DataRow(true, true, "0:45:00", "0 * * * *")]
    [DataRow(false, false, null, null)]
    [DataRow(false, true, "0:15:00", null)]
    [DataRow(false, true, null, "*/30 * * * *")] // every half an hour at :00 and :30
    [DataRow(false, true, "0:15:00", "0 * * * *")]
    public void IsDeleteAllowedForUploader(bool expected, bool enabled, string? duration, string? interval)
    {
        var deliveryDate = new DateTime(2026, 1, 1, 12, 10, 0, DateTimeKind.Utc);
        var currentDate = new DateTime(2026, 1, 1, 12, 40, 0, DateTimeKind.Utc);

        var options = new DeliveryOptions
        {
            UploaderDeleteEnabled = enabled,
            DeleteDuration = duration == null ? null : TimeSpan.Parse(duration, CultureInfo.InvariantCulture),
            DeleteRestrictInterval = interval,
        };
        deliveryOptionsMock.Setup(o => o.Value).Returns(options);

        Assert.AreEqual(expected, deliveryController.IsDeleteAllowedForUploader(deliveryDate, currentDate));
    }

    [TestMethod]
    [DataRow(DateTimeKind.Utc)]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public void IsDeleteAllowedForUploaderConvertsToUtc(DateTimeKind deliveryDateTimeKind)
    {
        var restrictedDate = new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc);
        var allowedDate = new DateTime(2026, 1, 1, 11, 45, 0, DateTimeKind.Utc);
        var deliveryDate = new DateTime(2026, 1, 1, 11, 30, 0, DateTimeKind.Utc);
        deliveryDate = deliveryDateTimeKind switch
        {
            DateTimeKind.Utc => deliveryDate,
            DateTimeKind.Local => deliveryDate.ToLocalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(deliveryDate, DateTimeKind.Unspecified),
            _ => throw new ArgumentOutOfRangeException(nameof(deliveryDateTimeKind), deliveryDateTimeKind, null),
        };

        var options = new DeliveryOptions
        {
            UploaderDeleteEnabled = true,
            DeleteDuration = null,
            DeleteRestrictInterval = "0 * * * *", // every hour at :00
        };
        deliveryOptionsMock.Setup(o => o.Value).Returns(options);

        Assert.IsFalse(deliveryController.IsDeleteAllowedForUploader(deliveryDate, restrictedDate));
        Assert.IsTrue(deliveryController.IsDeleteAllowedForUploader(deliveryDate, allowedDate));
    }

    [TestMethod]
    public void IsDeleteAllowedForUploaderClosesAtLocalMidnight()
    {
        var options = new DeliveryOptions
        {
            UploaderDeleteEnabled = true,
            DeleteRestrictInterval = "0 0 * * *",
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich"),
        };
        deliveryOptionsMock.Setup(o => o.Value).Returns(options);

        // Midnight in Zurich is 23:00 UTC in winter, an hour before midnight UTC.
        var deliveredAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        Assert.IsTrue(deliveryController.IsDeleteAllowedForUploader(deliveredAt, new DateTime(2026, 1, 15, 22, 59, 0, DateTimeKind.Utc)));
        Assert.IsFalse(deliveryController.IsDeleteAllowedForUploader(deliveredAt, new DateTime(2026, 1, 15, 23, 30, 0, DateTimeKind.Utc)));
    }

    [TestMethod]
    public void IsDeleteAllowedForUploaderStaysDisabledBeforeTheDelivery()
    {
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = false });

        var deliveredAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        Assert.IsFalse(deliveryController.IsDeleteAllowedForUploader(deliveredAt, deliveredAt.AddMinutes(-1)));
    }

    [TestMethod]
    public async Task Download()
    {
        WithoutDeleteWindow();
        assetHandlerMock.Setup(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Encoding.UTF8.GetBytes("Test"), "text/xml"));
        authorizationServiceMock
            .Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GeopilotPolicies.Admin))
            .ReturnsAsync(AuthorizationResult.Success());
        var delivery = CreateDeliveryWithAsset(context.Mandates.First(m => m.StacAccess == StacAccess.AdminsOnly));

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as FileContentResult;
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.FileContents);
        Assert.AreEqual("Test.xml", result.FileDownloadName);
        Assert.AreEqual("text/xml", result.ContentType);
    }

    [TestMethod]
    public async Task DownloadOfAPublicMandateNeedsNoAdmin()
    {
        WithoutDeleteWindow();
        assetHandlerMock.Setup(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Encoding.UTF8.GetBytes("Test"), "text/xml"));
        var delivery = CreateDeliveryWithAsset(context.Mandates.Single(m => m.StacAccess == StacAccess.Public));

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as FileContentResult;

        Assert.IsNotNull(result);
        Assert.AreEqual("Test.xml", result.FileDownloadName);
    }

    [TestMethod]
    public async Task DownloadOfAnAdminOnlyMandateIsNotFoundForNonAdmins()
    {
        var delivery = CreateDeliveryWithAsset(context.Mandates.First(m => m.StacAccess == StacAccess.AdminsOnly));

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
        assetHandlerMock.Verify(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DataRow(ReactionState.Pending, false, DisplayName = "Transfer pending, anonymous")]
    [DataRow(ReactionState.Pending, true, DisplayName = "Transfer pending, administrator")]
    [DataRow(ReactionState.Running, true, DisplayName = "Transfer running, administrator")]
    [DataRow(ReactionState.Error, true, DisplayName = "Transfer failed, administrator")]
    public async Task DownloadOfADeliveryThatIsNotForwardedIsNotFound(ReactionState transferState, bool asAdmin)
    {
        if (asAdmin)
        {
            authorizationServiceMock
                .Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GeopilotPolicies.Admin))
                .ReturnsAsync(AuthorizationResult.Success());
        }

        var delivery = CreateDeliveryWithAsset(context.Mandates.Single(m => m.StacAccess == StacAccess.Public));
        context.DeliveryReactions.Add(NewReaction(delivery, "drop", transferState));
        context.SaveChanges();

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
        assetHandlerMock.Verify(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task DownloadOfAForwardedDeliveryDoesNotWaitForItsNotifications()
    {
        WithoutDeleteWindow();
        assetHandlerMock.Setup(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Encoding.UTF8.GetBytes("Test"), "text/xml"));
        var delivery = CreateDeliveryWithAsset(context.Mandates.Single(m => m.StacAccess == StacAccess.Public));
        context.DeliveryReactions.AddRange(
            NewReaction(delivery, "drop", ReactionState.Done),
            NewReaction(delivery, "mail", ReactionState.Pending, ReactionKind.Notification));
        context.SaveChanges();

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as FileContentResult;

        Assert.AreEqual("Test.xml", result?.FileDownloadName);
    }

    [TestMethod]
    [DataRow(1, 0, DisplayName = "Window still open")]
    [DataRow(null, 2, DisplayName = "Window never closes")]
    public async Task DownloadOfADeliveryWithoutTransfersIsNotFoundWhileItsDeleteWindowIsOpen(int? windowHours, int deliveredHoursAgo)
    {
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = windowHours is { } hours ? TimeSpan.FromHours(hours) : null });
        var delivery = CreateDeliveryWithAsset(context.Mandates.Single(m => m.StacAccess == StacAccess.Public));
        delivery.Date = DateTime.UtcNow.AddHours(-deliveredHoursAgo);
        context.SaveChanges();

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
        assetHandlerMock.Verify(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task DownloadOfADeliveryWithoutTransfersIsFoundOnceItsDeleteWindowHasClosed()
    {
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromHours(1) });
        assetHandlerMock.Setup(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Encoding.UTF8.GetBytes("Test"), "text/xml"));
        var delivery = CreateDeliveryWithAsset(context.Mandates.Single(m => m.StacAccess == StacAccess.Public));
        delivery.Date = DateTime.UtcNow.AddHours(-2);
        context.SaveChanges();

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as FileContentResult;

        Assert.AreEqual("Test.xml", result?.FileDownloadName);
    }

    // Without uploader deletion there is no delete window, so a delivery is published as soon as its transfers are done.
    private void WithoutDeleteWindow() => deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = false });

    private Delivery CreateDeliveryWithAsset(Mandate mandate)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = mandate, DeliveredByUser = context.Users.First() };
        delivery.Assets.Add(new Asset() { OriginalFilename = "Test.xml", SanitizedFilename = "xyz.xml" });
        context.Deliveries.Add(delivery);
        context.SaveChanges();
        return delivery;
    }

    [TestMethod]
    public async Task DownloadFailsAssetNotFound()
    {
        var result = await deliveryController.DownloadAsync(context.Assets.Max(d => d.Id) + 1) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public async Task GetCountsAllDeliveriesWithoutFilters()
    {
        var page = await ListAsync(new DeliveryQuery());

        Assert.AreEqual(context.Deliveries.Count(), page.TotalCount);
        Assert.HasCount(Math.Min(page.TotalCount, 25), page.Items, "without a page size the list takes 25 a page.");
    }

    [TestMethod]
    public async Task GetReturnsTheNewestDeliveriesFirstOnePageAtATime()
    {
        var oldest = AddListedDelivery(minutes: 1);
        var sameTimeFirst = AddListedDelivery(minutes: 2);
        var sameTimeSecond = AddListedDelivery(minutes: 2);
        var newest = AddListedDelivery(minutes: 3);

        var first = await ListAsync(InListWindow(q => q.PageSize = 3));
        var second = await ListAsync(InListWindow(q =>
        {
            q.PageSize = 3;
            q.Page = 1;
        }));

        Assert.AreEqual(4, first.TotalCount);
        Assert.AreEqual(4, second.TotalCount);
        CollectionAssert.AreEqual(
            new[] { newest.Id, sameTimeSecond.Id, sameTimeFirst.Id },
            first.Ids(),
            "deliveries made at the same time come with the higher id first.");
        CollectionAssert.AreEqual(new[] { oldest.Id }, second.Ids());
    }

    [TestMethod]
    public async Task GetFiltersByPeriodWithTheStartIncludedAndTheEndExcluded()
    {
        AddListedDelivery(minutes: 9);
        var atStart = AddListedDelivery(minutes: 10);
        var beforeEnd = AddListedDelivery(minutes: 19);
        AddListedDelivery(minutes: 20);

        var page = await ListAsync(new DeliveryQuery { From = ListWindow.AddMinutes(10), To = ListWindow.AddMinutes(20) });

        CollectionAssert.AreEquivalent(new[] { atStart.Id, beforeEnd.Id }, page.Ids());
    }

    [TestMethod]
    public async Task GetReadsAPeriodWithoutOffsetAsUtc()
    {
        var delivery = AddListedDelivery(minutes: 10);
        var unspecified = DateTime.SpecifyKind(ListWindow.AddMinutes(10), DateTimeKind.Unspecified);

        var page = await ListAsync(new DeliveryQuery { From = unspecified, To = unspecified.AddMinutes(1) });

        CollectionAssert.AreEqual(new[] { delivery.Id }, page.Ids());
    }

    [TestMethod]
    public async Task GetFiltersByEveryChosenMandate()
    {
        var mandates = context.Mandates.OrderBy(m => m.Id).Take(3).ToList();
        var first = AddListedDelivery(minutes: 1, d => d.Mandate = mandates[0]);
        AddListedDelivery(minutes: 2, d => d.Mandate = mandates[1]);
        var third = AddListedDelivery(minutes: 3, d => d.Mandate = mandates[2]);

        var page = await ListAsync(InListWindow(q => q.MandateIds = [mandates[0].Id, mandates[2].Id]));

        CollectionAssert.AreEquivalent(new[] { first.Id, third.Id }, page.Ids());
    }

    [TestMethod]
    public async Task GetFiltersBySupplierAsPersonAndAsMachineClient()
    {
        var users = context.Users.Where(u => !u.IsAdmin).OrderBy(u => u.Id).Take(2).ToList();
        var chosenClient = AddMachineClient(MachineClientState.Active);
        var otherClient = AddMachineClient(MachineClientState.Active);
        var byChosenUser = AddListedDelivery(minutes: 1, d => d.DeliveredByUser = users[0]);
        AddListedDelivery(minutes: 2, d => d.DeliveredByUser = users[1]);
        var byChosenClient = AddListedDelivery(minutes: 3, d =>
        {
            d.DeliveredByUser = null;
            d.DeliveredByClient = chosenClient;
        });
        AddListedDelivery(minutes: 4, d =>
        {
            d.DeliveredByUser = null;
            d.DeliveredByClient = otherClient;
        });

        var both = await ListAsync(InListWindow(q =>
        {
            q.UserIds = [users[0].Id];
            q.MachineClientIds = [chosenClient.Id];
        }));
        var personOnly = await ListAsync(InListWindow(q => q.UserIds = [users[0].Id]));
        var clientOnly = await ListAsync(InListWindow(q => q.MachineClientIds = [chosenClient.Id]));

        CollectionAssert.AreEquivalent(new[] { byChosenUser.Id, byChosenClient.Id }, both.Ids(), "persons and machine clients together make up the filter by supplier.");
        CollectionAssert.AreEqual(new[] { byChosenUser.Id }, personOnly.Ids());
        CollectionAssert.AreEqual(new[] { byChosenClient.Id }, clientOnly.Ids());
    }

    [TestMethod]
    public async Task GetFiltersByTheBeginningOfTheId()
    {
        // Ids far above those the database hands out while the tests run; 8642 stands for the 12 of the spec.
        var same = AddListedDelivery(minutes: 1, d => d.Id = 8642);
        var longer = AddListedDelivery(minutes: 2, d => d.Id = 86420);
        var muchLonger = AddListedDelivery(minutes: 3, d => d.Id = 8642123);
        AddListedDelivery(minutes: 4, d => d.Id = 18642);

        var page = await ListAsync(InListWindow(q => q.IdPrefix = "8642"));

        CollectionAssert.AreEquivalent(new[] { same.Id, longer.Id, muchLonger.Id }, page.Ids());
    }

    [TestMethod]
    public async Task GetFiltersByFailedForwardingOrNotifications()
    {
        var failedForwarding = AddListedDelivery(minutes: 1);
        var failedNotification = AddListedDelivery(minutes: 2);
        var failedBoth = AddListedDelivery(minutes: 3);
        var underway = AddListedDelivery(minutes: 4);
        var forwarded = AddListedDelivery(minutes: 5);
        AddListedDelivery(minutes: 6);
        context.DeliveryReactions.AddRange(
            NewReaction(failedForwarding, "ablage", ReactionState.Error),
            NewReaction(failedForwarding, "meldung", ReactionState.Pending, ReactionKind.Notification),
            NewReaction(failedNotification, "ablage", ReactionState.Done),
            NewReaction(failedNotification, "meldung", ReactionState.Error, ReactionKind.Notification),
            NewReaction(failedBoth, "ablage", ReactionState.Error),
            NewReaction(failedBoth, "meldung", ReactionState.Error, ReactionKind.Notification),
            NewReaction(underway, "ablage", ReactionState.Running),
            NewReaction(underway, "archiv", ReactionState.Pending, attempts: 3),
            NewReaction(forwarded, "ablage", ReactionState.Done),
            NewReaction(forwarded, "meldung", ReactionState.Done, ReactionKind.Notification));
        context.SaveChanges();

        var forwarding = await ListAsync(InListWindow(q => q.FailedReactionKinds = [ReactionKind.Transfer]));
        var notifications = await ListAsync(InListWindow(q => q.FailedReactionKinds = [ReactionKind.Notification]));
        var either = await ListAsync(InListWindow(q => q.FailedReactionKinds = [ReactionKind.Transfer, ReactionKind.Notification]));

        CollectionAssert.AreEquivalent(new[] { failedForwarding.Id, failedBoth.Id }, forwarding.Ids(), "a failed notification is no failed forwarding, nor is a reaction that still tries.");
        CollectionAssert.AreEquivalent(new[] { failedNotification.Id, failedBoth.Id }, notifications.Ids());
        CollectionAssert.AreEquivalent(new[] { failedForwarding.Id, failedNotification.Id, failedBoth.Id }, either.Ids());
    }

    [TestMethod]
    public async Task GetFiltersFailedExactlyWhereTheColumnShowsIt()
    {
        ReactionState[] states = [ReactionState.Pending, ReactionState.Running, ReactionState.Error, ReactionState.Done];

        // Every combination of the four states, none at all included, as transfers, and the states left over as
        // notifications, so that mixing up the kinds shows.
        var deliveries = Enumerable.Range(0, 1 << states.Length)
            .Select(combination =>
            {
                var transfers = states.Where((_, index) => (combination & (1 << index)) != 0).ToArray();
                var notifications = states.Except(transfers).ToArray();
                var delivery = AddListedDelivery(minutes: combination);
                context.DeliveryReactions.AddRange(transfers.Select(state => NewReaction(delivery, $"ablage_{state}", state)));
                context.DeliveryReactions.AddRange(notifications.Select(state => NewReaction(delivery, $"meldung_{state}", state, ReactionKind.Notification)));
                return (delivery.Id, Forwarding: transfers.SummedUp(), Notification: notifications.SummedUp(), Transfers: string.Join(",", transfers));
            })
            .ToList();
        context.SaveChanges();

        var column = (await ListAsync(InListWindow())).Items.ToDictionary(d => d.Id);
        var failedForwarding = await ListAsync(InListWindow(q => q.FailedReactionKinds = [ReactionKind.Transfer]));
        var failedNotifications = await ListAsync(InListWindow(q => q.FailedReactionKinds = [ReactionKind.Notification]));

        foreach (var delivery in deliveries)
        {
            Assert.AreEqual(delivery.Forwarding, column[delivery.Id].ForwardingState, $"forwarding of transfers <{delivery.Transfers}>");
            Assert.AreEqual(delivery.Notification, column[delivery.Id].NotificationState, $"notifications of all but <{delivery.Transfers}>");
        }

        CollectionAssert.AreEquivalent(deliveries.Where(d => d.Forwarding == ReactionState.Error).Select(d => d.Id).ToArray(), failedForwarding.Ids());
        CollectionAssert.AreEquivalent(deliveries.Where(d => d.Notification == ReactionState.Error).Select(d => d.Id).ToArray(), failedNotifications.Ids());
    }

    [TestMethod]
    public async Task GetNarrowsTheListByEveryFilterThatIsSet()
    {
        var mandates = context.Mandates.OrderBy(m => m.Id).Take(2).ToList();
        var users = context.Users.Where(u => !u.IsAdmin).OrderBy(u => u.Id).Take(2).ToList();
        Delivery AddFailed(int minutes, Action<Delivery>? differ = null, ReactionState transfer = ReactionState.Error)
        {
            var delivery = AddListedDelivery(minutes, d =>
            {
                d.Mandate = mandates[0];
                d.DeliveredByUser = users[0];
                differ?.Invoke(d);
            });
            context.DeliveryReactions.Add(NewReaction(delivery, "ablage", transfer));
            context.SaveChanges();
            return delivery;
        }

        var match = AddFailed(minutes: 10);
        AddFailed(minutes: 5);
        AddFailed(minutes: 11, d => d.Mandate = mandates[1]);
        AddFailed(minutes: 12, d => d.DeliveredByUser = users[1]);
        AddFailed(minutes: 13, transfer: ReactionState.Done);

        var page = await ListAsync(new DeliveryQuery
        {
            From = ListWindow.AddMinutes(10),
            To = ListWindow.AddMinutes(20),
            MandateIds = [mandates[0].Id],
            UserIds = [users[0].Id],
            FailedReactionKinds = [ReactionKind.Transfer],
        });
        var byId = await ListAsync(InListWindow(q =>
        {
            q.IdPrefix = match.Id.ToString(CultureInfo.InvariantCulture);
            q.FailedReactionKinds = [ReactionKind.Transfer];
        }));

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual(match.Id, page.Items.Single().Id);
        Assert.AreEqual(match.Id, byId.Items.Single().Id);
    }

    [TestMethod]
    public async Task GetLeavesOutDeletedDeliveries()
    {
        var kept = AddListedDelivery(minutes: 1);
        AddListedDelivery(minutes: 2, d => d.Deleted = true);

        var page = await ListAsync(InListWindow());

        Assert.AreEqual(1, page.TotalCount);
        CollectionAssert.AreEqual(new[] { kept.Id }, page.Ids());
    }

    [TestMethod]
    public async Task GetListsEachDeliveryWithTheStatesOfItsForwardingAndNotifications()
    {
        var supplier = context.Users.First(u => !u.IsAdmin);
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        var forwarded = AddListedDelivery(minutes: 1, d => d.DeliveredByUser = supplier);
        var failed = AddListedDelivery(minutes: 2, d =>
        {
            d.DeliveredByUser = null;
            d.DeliveredByClient = client;
        });
        var withoutReactions = AddListedDelivery(minutes: 3, d => d.DeliveredByUser = supplier);
        context.DeliveryReactions.AddRange(
            NewReaction(forwarded, "ablage", ReactionState.Done),
            NewReaction(forwarded, "meldung", ReactionState.Pending, ReactionKind.Notification),
            NewReaction(failed, "ablage", ReactionState.Error),
            NewReaction(failed, "archiv", ReactionState.Pending));
        context.SaveChanges();

        var list = (await ListAsync(InListWindow())).Items;

        var byId = list.ToDictionary(d => d.Id);
        var forwardedResponse = byId[forwarded.Id];
        Assert.AreEqual(supplier.FullName, forwardedResponse.DeliveredByName);
        Assert.IsNull(forwardedResponse.DeliveredByClientId, "a delivery a user made names no client.");
        Assert.AreEqual(forwarded.Mandate.Id, forwardedResponse.Mandate.Id);
        Assert.AreEqual(ReactionState.Done, forwardedResponse.ForwardingState, "a notification does not hold up the forwarding.");
        Assert.AreEqual(ReactionState.Pending, forwardedResponse.NotificationState);
        Assert.AreEqual("SILENTHARBOR", byId[failed.Id].DeliveredByName);
        Assert.AreEqual(client.Id, byId[failed.Id].DeliveredByClientId);
        Assert.AreEqual(ReactionState.Error, byId[failed.Id].ForwardingState);
        Assert.IsNull(byId[failed.Id].NotificationState, "a delivery without notifications has none to sum up.");
        Assert.IsNull(byId[withoutReactions.Id].ForwardingState);
        Assert.IsNull(byId[withoutReactions.Id].NotificationState);
    }

    [TestMethod]
    public async Task GetByIdNamesTheReactionsAfterTheDefinition()
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        pipelineFactoryMock.SetupGet(factory => factory.ReactionSets).Returns(
        [
            new ReactionSetConfig
            {
                Id = "kanton_a",
                DisplayName = TestHelpers.Localized("Kanton A"),
                Transfers = [new ReactionConfig { Id = "ablage", DisplayName = TestHelpers.Localized("Ablage"), ChannelId = "ablage" }],
                Notifications = [new ReactionConfig { Id = "meldung", DisplayName = TestHelpers.Localized("Info-Mail"), ChannelId = "mail" }],
            },
        ]);
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = context.Users.First(u => !u.IsAdmin) };
        context.Deliveries.Add(delivery);
        context.DeliveryReactions.AddRange(
            NewReaction(delivery, "ablage", ReactionState.Done),
            NewReaction(delivery, "meldung", ReactionState.Done, ReactionKind.Notification),
            NewReaction(delivery, "entfernt", ReactionState.Done));
        context.SaveChanges();

        var detail = ((await deliveryController.GetById(delivery.Id)) as ObjectResult)?.Value as DeliveryDetailResponse;

        var reactions = detail?.Reactions;
        Assert.IsNotNull(reactions);
        Assert.AreEqual(TestHelpers.Localized("Ablage"), reactions[0].DisplayName);
        Assert.AreEqual(TestHelpers.Localized("Info-Mail"), reactions[1].DisplayName);
        Assert.IsNull(reactions[2].DisplayName, "a reaction the definition no longer has is shown by its id only.");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Unknown delivery")]
    [DataRow(true, DisplayName = "Deleted delivery")]
    public async Task ResetReactionOfADeliveryThatDoesNotExistIsNotFound(bool deleted)
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Error));
        delivery.Deleted = deleted;
        context.SaveChanges();
        var deliveryId = deleted ? delivery.Id : context.Deliveries.IgnoreQueryFilters().Max(d => d.Id) + 1;

        var result = await deliveryController.ResetReaction(deliveryId, "ablage") as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
        Assert.AreEqual(ReactionState.Error, context.DeliveryReactions.IgnoreQueryFilters().AsNoTracking().Single(r => r.DeliveryId == delivery.Id).State);
        recorderMock.Verify(recorder => recorder.RecordReactionsResetAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<DeliveryReaction>>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task ResetReactionMakesOnlyThatReactionDueAgain()
    {
        var admin = context.Users.First(u => u.IsAdmin);
        deliveryController.SetupTestUser(admin);
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Error), ("archiv", ReactionState.Error));

        Assert.IsInstanceOfType<NoContentResult>(await deliveryController.ResetReaction(delivery.Id, "ablage"));

        var states = context.DeliveryReactions
            .AsNoTracking()
            .Where(r => r.DeliveryId == delivery.Id)
            .ToDictionary(r => r.ReactionId, r => r.State);
        Assert.AreEqual(ReactionState.Pending, states["ablage"]);
        Assert.AreEqual(ReactionState.Error, states["archiv"]);
        recorderMock.Verify(
            recorder => recorder.RecordReactionsResetAsync(
                delivery.Id,
                It.Is<IReadOnlyList<DeliveryReaction>>(reactions => reactions.Count == 1 && reactions[0].ReactionId == "ablage"),
                admin.Id),
            Times.Once);
    }

    [TestMethod]
    public async Task ResetReactionThatHasNotFailedIsAConflict()
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Done), ("archiv", ReactionState.Error));

        var result = await deliveryController.ResetReaction(delivery.Id, "ablage") as ObjectResult;

        Assert.AreEqual(StatusCodes.Status409Conflict, result?.StatusCode);
        Assert.AreEqual(ReactionState.Error, context.DeliveryReactions.AsNoTracking().Single(r => r.DeliveryId == delivery.Id && r.ReactionId == "archiv").State, "another failed reaction stays as it is.");
        recorderMock.Verify(recorder => recorder.RecordReactionsResetAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<DeliveryReaction>>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task ResetReactionThatTheDeliveryDoesNotHaveIsNotFound()
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Error));

        var result = await deliveryController.ResetReaction(delivery.Id, "archiv") as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
        recorderMock.Verify(recorder => recorder.RecordReactionsResetAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<DeliveryReaction>>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task CreateStoresTheMetadataOfTheRequest()
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateStoresTheMetadataOfTheRequest)),
                EvaluateComment = FieldEvaluationType.NotEvaluated,
                EvaluatePartial = FieldEvaluationType.NotEvaluated,
                EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
                AllowDelivery = true,
                MetadataFields = [new MetadataField("project_number", TestHelpers.Localized("Project number"), MetadataFieldType.Text, FieldEvaluationType.Required)],
            });
        deliveryController.SetupTestUser(user);
        Guid jobId = SetupProcessingJob(mandate.Id);
        SetupJobPersistence(jobId);

        var request = new DeliveryRequest
        {
            JobId = jobId,
            Metadata = new Dictionary<string, JsonElement> { ["project_number"] = JsonSerializer.SerializeToElement("P-17") },
        };

        var result = (await deliveryController.Create(request)) as ObjectResult;

        context.ChangeTracker.Clear();
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
        var dbDelivery = context.Deliveries.AsNoTracking().Single(d => d.JobId == jobId);
        Assert.AreEqual("P-17", Assert.ContainsSingle(dbDelivery.Metadata).Value?.GetString());
    }

    [TestMethod]
    public async Task CreateRefusesAMissingRequiredMetadataValue()
    {
        var (user, mandate) = SetupMandateWithUserOrganisation(
            new Mandate
            {
                Name = TestHelpers.Localized(nameof(CreateRefusesAMissingRequiredMetadataValue)),
                EvaluateComment = FieldEvaluationType.NotEvaluated,
                EvaluatePartial = FieldEvaluationType.NotEvaluated,
                EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
                AllowDelivery = true,
                MetadataFields = [new MetadataField("project_number", TestHelpers.Localized("Project number"), MetadataFieldType.Text, FieldEvaluationType.Required)],
            });
        deliveryController.SetupTestUser(user);
        Guid jobId = SetupProcessingJob(mandate.Id);

        // A request that bypasses the form: no metadata at all.
        var result = await deliveryController.Create(new DeliveryRequest { JobId = jobId });

        var problem = Assert.IsInstanceOfType<ValidationProblemDetails>((result as ObjectResult)?.Value);
        Assert.IsTrue(problem.Errors.ContainsKey("metadata.project_number"));
        Assert.IsFalse(context.Deliveries.Any(d => d.JobId == jobId));
        AssertDeliveryFailureRecorded(jobId, "The delivery details do not satisfy the mandate. metadata.project_number: The metadata field <project_number> is required for this mandate.");
    }

    [TestMethod]
    public async Task GetByIdReturnsTheMetadataCopy()
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var supplier = context.Users.First(u => !u.IsAdmin);
        var delivery = new Delivery
        {
            JobId = Guid.NewGuid(),
            Mandate = context.Mandates.First(),
            DeliveredByUser = supplier,
            Metadata =
            [
                new DeliveredMetadataField("project_number", TestHelpers.Localized("Project number"), MetadataFieldType.Text, FieldEvaluationType.Optional, null, JsonSerializer.SerializeToElement("P-17")),
                new DeliveredMetadataField("remark", TestHelpers.Localized("Remark"), MetadataFieldType.Text, FieldEvaluationType.Optional, null, null),
            ],
        };
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        var detail = ((await deliveryController.GetById(delivery.Id)) as ObjectResult)?.Value as DeliveryDetailResponse;

        Assert.IsNotNull(detail);
        Assert.HasCount(2, detail.Metadata);
        Assert.AreEqual("P-17", detail.Metadata[0].Value?.GetString());
        Assert.IsNull(detail.Metadata[1].Value);
    }

    [TestMethod]
    public async Task GetByIdShowsWhatWasDelivered()
    {
        WithoutDeleteWindow();
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var supplier = context.Users.First(u => !u.IsAdmin);
        var precursor = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = supplier };
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = supplier, PrecursorDelivery = precursor, Partial = true, Comment = "zweite Lieferung" };
        delivery.Assets.Add(new Asset { OriginalFilename = "Strassen 2026.xtf", SanitizedFilename = "Strassen_2026.xtf" });
        delivery.Assets.Add(new Asset { OriginalFilename = "errorLog.log", SanitizedFilename = "errorLog.log" });
        context.Deliveries.AddRange(precursor, delivery);
        context.DeliveryReactions.AddRange(
            NewReaction(delivery, "ablage", ReactionState.Done),
            NewReaction(delivery, "meldung", ReactionState.Pending, ReactionKind.Notification));
        context.SaveChanges();

        var detail = ((await deliveryController.GetById(delivery.Id)) as ObjectResult)?.Value as DeliveryDetailResponse;

        Assert.IsNotNull(detail);
        Assert.AreEqual(supplier.FullName, detail.DeliveredByName);
        Assert.AreEqual(supplier.Id, detail.DeliveredByUserId);
        Assert.IsNull(detail.DeliveredByClientId);
        Assert.AreEqual(delivery.Mandate.Id, detail.Mandate.Id);
        Assert.AreEqual(precursor.Id, detail.PrecursorDelivery?.Id);
        Assert.IsTrue(detail.Partial);
        Assert.AreEqual(delivery.JobId, detail.JobId);
        CollectionAssert.AreEqual(new[] { "Strassen 2026.xtf", "errorLog.log" }, detail.Files.Select(f => f.FileName).ToArray());
        CollectionAssert.AreEqual(delivery.Assets.Select(a => a.Id).ToArray(), detail.Files.Select(f => f.Id).ToArray());
        Assert.AreEqual("zweite Lieferung", detail.Comment);
        Assert.AreEqual(new StacItemReference($"coll_{delivery.Mandate.Id}", $"item_{delivery.Id}"), detail.StacItem, "a waiting notification does not keep a forwarded delivery out of the catalog.");
        CollectionAssert.AreEqual(new[] { "ablage", "meldung" }, detail.Reactions.Select(r => r.ReactionId).ToArray());
        Assert.IsNull(detail.Reactions[0].NextAttemptAt, "a reaction that is done has no next attempt.");
        Assert.IsNotNull(detail.Reactions[1].NextAttemptAt);
    }

    [TestMethod]
    [DataRow(ReactionState.Pending)]
    [DataRow(ReactionState.Error)]
    public async Task GetByIdLinksNoStacItemBeforeTheDeliveryIsForwarded(ReactionState transferState)
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Done), ("archiv", transferState));

        var detail = ((await deliveryController.GetById(delivery.Id)) as ObjectResult)?.Value as DeliveryDetailResponse;

        Assert.IsNotNull(detail);
        Assert.IsNull(detail.StacItem);
        Assert.IsNull(detail.PrecursorDelivery);
    }

    [TestMethod]
    public async Task GetByIdLinksNoStacItemWhileTheDeleteWindowIsOpen()
    {
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromHours(1) });
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions();

        var detail = ((await deliveryController.GetById(delivery.Id)) as ObjectResult)?.Value as DeliveryDetailResponse;

        Assert.IsNotNull(detail);
        Assert.IsNull(detail.StacItem, "a delivery without transfers is only published once its uploader can no longer delete it.");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Unknown delivery")]
    [DataRow(true, DisplayName = "Deleted delivery")]
    public async Task GetByIdOfADeliveryThatDoesNotExistIsNotFound(bool deleted)
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions();
        delivery.Deleted = deleted;
        context.SaveChanges();
        var deliveryId = deleted ? delivery.Id : context.Deliveries.IgnoreQueryFilters().Max(d => d.Id) + 1;

        var result = await deliveryController.GetById(deliveryId) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
    }

    private async Task<DeliveryPageResponse> ListAsync(DeliveryQuery query)
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        return ActionResultAssert.IsOkObjectResult<DeliveryPageResponse>(await deliveryController.Get(query, CancellationToken.None));
    }

    // The deliveries of the list tests lie in a window of their own, so the seeded ones do not count.
    private static DeliveryQuery InListWindow(Action<DeliveryQuery>? narrow = null)
    {
        var query = new DeliveryQuery { From = ListWindow, To = ListWindow.AddDays(1), PageSize = 100 };
        narrow?.Invoke(query);
        return query;
    }

    private Delivery AddListedDelivery(int minutes, Action<Delivery>? setup = null)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = context.Users.First(u => !u.IsAdmin), Date = ListWindow.AddMinutes(minutes) };
        setup?.Invoke(delivery);
        context.Deliveries.Add(delivery);
        context.SaveChanges();
        return delivery;
    }

    private Delivery AddDeliveryWithReactions(params (string ReactionId, ReactionState State)[] reactions)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByUser = context.Users.First(u => !u.IsAdmin) };
        context.Deliveries.Add(delivery);
        context.DeliveryReactions.AddRange(reactions.Select(reaction => NewReaction(delivery, reaction.ReactionId, reaction.State)));
        context.SaveChanges();
        return delivery;
    }

    [TestMethod]
    public async Task GetSummaryReturnsNotFoundForUnauthorizedMandate()
    {
        var user = context.Users.First();
        deliveryController.SetupTestUser(user);
        var mandateId = context.Mandates
            .Where(m => !m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id))
            .First()
            .Id;
        SetupGetMandateForUser(mandateId, user, returnNull: true);

        var response = await deliveryController.GetSummary(mandateId);

        Assert.IsInstanceOfType<NotFoundResult>(response);
    }

    [TestMethod]
    public async Task GetSummaryReturnsListFilteredByMandateId()
    {
        var user = context.Users.First(u => u.Organisations.SelectMany(o => o.Mandates).Any(o => o.Deliveries.Count != 0));
        deliveryController.SetupTestUser(user);
        var mandate = context.Mandates
            .Where(m => m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id) && m.Deliveries.Count != 0)
            .First();
        SetupGetMandateForUser(mandate.Id, user);

        var response = (await deliveryController.GetSummary(mandate.Id)) as ObjectResult;
        var list = Assert.IsInstanceOfType<List<DeliverySummary>>(response?.Value);

        var deliveries = context.Mandates
            .Include(m => m.Deliveries)
            .First(m => m.Id == mandate.Id)
            .Deliveries;

        Assert.IsNotNull(list);
        Assert.AreNotEqual(0, deliveries.Count);
        Assert.HasCount(deliveries.Count, list);
        CollectionAssert.AllItemsAreUnique(list);
    }

    [TestMethod]
    public async Task GetUploadsReturnsDeliveriesOfUserInTheOrderTheyWereCreated()
    {
        var user = context.Users.First(u => !u.IsAdmin && context.Deliveries.Count(d => d.DeliveredByUserId == u.Id) >= 2);
        deliveryController.SetupTestUser(user);

        // An update moves the row within the table, so the database no longer returns the deliveries in id order by chance.
        var first = context.Deliveries.Where(d => d.DeliveredByUserId == user.Id).OrderBy(d => d.Id).First();
        first.Comment += " (edited)";
        context.SaveChanges();

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var uploads = Assert.IsInstanceOfType<List<UploadedDelivery>>(response?.Value);
        var expected = context.Deliveries
            .Include(d => d.Mandate)
            .Where(d => d.DeliveredByUserId == user.Id)
            .OrderBy(d => d.Id)
            .AsEnumerable()
            .Select(d => (d.Id, d.Date, d.Comment, Mandate: new MandateReference(d.Mandate.Id, d.Mandate.Name)))
            .ToList();
        CollectionAssert.AreEqual(expected, uploads.Select(u => (u.Id, u.Date, u.Comment, u.Mandate)).ToList());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task GetUploadsReportsWhetherTheUploaderMayDelete(bool enabled)
    {
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = enabled });
        deliveryController.SetupTestUser(UserWithDeliveries());

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var uploads = Assert.IsInstanceOfType<List<UploadedDelivery>>(response?.Value);
        Assert.IsNotEmpty(uploads);
        Assert.IsTrue(uploads.All(upload => upload.CanDelete == enabled));
    }

    [TestMethod]
    public async Task GetUploadsReportsThatADeliveryWhoseForwardingBeganCannotBeDeleted()
    {
        var user = context.Users.First(u => !u.IsAdmin && context.Deliveries.Count(d => d.DeliveredByUserId == u.Id) >= 2);
        deliveryController.SetupTestUser(user);
        var forwarded = context.Deliveries.Where(d => d.DeliveredByUserId == user.Id).OrderBy(d => d.Id).First();
        context.DeliveryReactions.Add(NewReaction(forwarded, "ablage", ReactionState.Done, attempts: 1));
        context.SaveChanges();

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var uploads = Assert.IsInstanceOfType<List<UploadedDelivery>>(response?.Value);
        Assert.IsFalse(uploads.Single(upload => upload.Id == forwarded.Id).CanDelete);
        Assert.IsTrue(uploads.Where(upload => upload.Id != forwarded.Id).All(upload => upload.CanDelete));
    }

    [TestMethod]
    public async Task GetUploadsAddsTheDeliveriesOfTheClientsTheUserIsResponsibleFor()
    {
        var user = UserWithDeliveries();
        deliveryController.SetupTestUser(user);
        var client = AddMachineClient(MachineClientState.Inactive, user);
        var clientDelivery = AddClientDelivery(client);

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var uploads = Assert.IsInstanceOfType<List<UploadedDelivery>>(response?.Value);
        Assert.AreEqual(client.Name, uploads.Single(upload => upload.Id == clientDelivery.Id).DeliveredByName, "A deactivated client's deliveries stay with the persons responsible for it.");
        Assert.IsTrue(uploads.Where(upload => upload.Id != clientDelivery.Id).All(upload => upload.DeliveredByName == user.FullName));
    }

    [TestMethod]
    public async Task GetUploadsLeavesOutTheDeliveriesOfAClientOfTheSameOrganisation()
    {
        var user = UserWithDeliveries();
        deliveryController.SetupTestUser(user);
        var client = AddMachineClient(MachineClientState.Active);
        context.Organisations.Add(new Organisation { Name = "SHARED HARBOUR", Users = [user], MachineClients = [client] });
        context.SaveChanges();
        var clientDelivery = AddClientDelivery(client);

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var uploads = Assert.IsInstanceOfType<List<UploadedDelivery>>(response?.Value);
        Assert.IsFalse(uploads.Any(upload => upload.Id == clientDelivery.Id), "Only an explicit responsibility shows a client's deliveries, not a shared organisation.");
    }

    [TestMethod]
    public async Task DeleteAsResponsibleUser()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);
        var delivery = AddClientDelivery(AddMachineClient(MachineClientState.Active, user));
        assetHandlerMock.Setup(h => h.DeleteJobAssets(delivery.JobId));

        Assert.IsInstanceOfType<OkResult>(await deliveryController.Delete(delivery.Id));

        Assert.IsTrue(context.Deliveries.IgnoreQueryFilters().AsNoTracking().Single(d => d.Id == delivery.Id).Deleted);
    }

    [TestMethod]
    public async Task DeleteAsResponsibleUserFailsOnceTheWindowClosed()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);
        var delivery = AddClientDelivery(AddMachineClient(MachineClientState.Active, user), DateTime.UtcNow.AddHours(-12));
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromHours(10) });

        var result = Assert.IsInstanceOfType<ObjectResult>(await deliveryController.Delete(delivery.Id));

        Assert.AreEqual(StatusCodes.Status403Forbidden, result.StatusCode);
        AssertNotDeleted(delivery);
    }

    [TestMethod]
    public async Task DeleteAsUserFailsForAClientTheUserIsNotResponsibleFor()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        var responsible = context.Users.First(u => !u.IsAdmin && u.Id != user.Id);
        deliveryController.SetupTestUser(user);
        var delivery = AddClientDelivery(AddMachineClient(MachineClientState.Active, responsible));

        var result = Assert.IsInstanceOfType<ObjectResult>(await deliveryController.Delete(delivery.Id));

        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
        AssertNotDeleted(delivery);
    }

    private User UserWithDeliveries()
        => context.Users.First(user => !user.IsAdmin && context.Deliveries.Any(delivery => delivery.DeliveredByUserId == user.Id));

    private MachineClient AddMachineClient(MachineClientState state, params User[] responsibleUsers)
    {
        var client = context.MachineClients.Add(new MachineClient
        {
            AuthIdentifier = Guid.NewGuid().ToString(),
            Name = "Lieferroboter",
            State = state,
            ResponsibleUsers = responsibleUsers.ToList(),
        }).Entity;
        context.SaveChanges();
        return client;
    }

    private Delivery AddClientDelivery(MachineClient client, DateTime? date = null)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeliveredByClient = client, Date = date ?? DateTime.UtcNow };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();
        return delivery;
    }

    private static DeliveryReaction NewReaction(Delivery delivery, string reactionId, ReactionState state, ReactionKind kind = ReactionKind.Transfer, int attempts = 0) => new()
    {
        Delivery = delivery,
        ReactionId = reactionId,
        Kind = kind,
        Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
        State = state,
        Attempts = attempts,
        NextAttemptAt = DateTime.UtcNow,
    };

    private void AssertNotDeleted(Delivery delivery)
    {
        var dbDelivery = context.DeliveriesWithIncludes
            .IgnoreQueryFilters()
            .FirstOrDefault(d => d.Id == delivery.Id);
        Assert.IsNotNull(dbDelivery);
        Assert.IsFalse(dbDelivery.Deleted);
        Assert.IsTrue(dbDelivery.Assets.All(a => !a.Deleted));

        assetHandlerMock.Verify(h => h.DeleteJobAssets(It.IsAny<Guid>()), Times.Never());
    }

    private (User User, Mandate Mandate) SetupMandateWithUserOrganisation(Mandate mandate)
    {
        var (user, createdMandate) = context.AddMandateWithUserOrganisation(mandate);

        // A member, so the rule comes down to whether the mandate takes deliveries.
        SetupMayDeliver(createdMandate.Id, user, createdMandate.AllowDelivery);
        return (user, createdMandate);
    }

    private void SetupMayDeliver(int mandateId, User user, bool mayDeliver)
        => mandateServiceMock.Setup(s => s.MayDeliverAsync(mandateId, Supplier.ForUser(user.Id))).ReturnsAsync(mayDeliver);

    private void SetupGetMandateForUser(int mandateId, User user, bool returnNull = false)
    {
        // return a mandate loaded with AsNoTracking, like the MandateService
        var detachedMandate = returnNull
            ? null
            : context.Mandates.AsNoTracking().First(m => m.Id == mandateId);

        mandateServiceMock
            .Setup(s => s.GetMandateForDeliveryAsync(mandateId, Supplier.ForUser(user.Id)))
            .ReturnsAsync(detachedMandate);
    }
}

// The ids on a page of the list, in their order.
file static class DeliveryPageResponseExtensions
{
    internal static int[] Ids(this DeliveryPageResponse page) => page.Items.Select(d => d.Id).ToArray();
}
