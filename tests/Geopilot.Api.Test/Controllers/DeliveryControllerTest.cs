using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Reactions;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
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

namespace Geopilot.Api.Controllers;

[TestClass]
public class DeliveryControllerTest
{
    private Mock<IProcessingService> processingServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private Mock<ILogger<DeliveryController>> loggerMock;
    private Mock<ILogger<DeliveryDeclarationService>> declarationLoggerMock;
    private Mock<IOptions<DeliveryOptions>> deliveryOptionsMock;
    private Mock<IAuthorizationService> authorizationServiceMock;
    private Mock<IPipelineRunRecorder> recorderMock;
    private DeliveryController deliveryController;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<DeliveryController>>();
        declarationLoggerMock = new Mock<ILogger<DeliveryDeclarationService>>();
        processingServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        assetHandlerMock = new Mock<IAssetHandler>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        deliveryOptionsMock = new Mock<IOptions<DeliveryOptions>>();
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true });

        // The declaration service is wired up for real, not mocked: these tests cover the delivery rules, which
        // moved into it, and mocking it here would leave those rules untested.
        var declarationService = new DeliveryDeclarationService(declarationLoggerMock.Object, context, processingServiceMock.Object, mandateServiceMock.Object, assetHandlerMock.Object, Mock.Of<IPipelineFactory>(), deliveryOptionsMock.Object);
        authorizationServiceMock = new Mock<IAuthorizationService>();
        authorizationServiceMock
            .Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GeopilotPolicies.Admin))
            .ReturnsAsync(AuthorizationResult.Failed());
        recorderMock = new Mock<IPipelineRunRecorder>();
        var reactionStore = new ReactionStore(Options.Create(new DeliveryReactionOptions()), NullLogger<ReactionStore>.Instance);
        deliveryController = new DeliveryController(loggerMock.Object, context, declarationService, mandateServiceMock.Object, assetHandlerMock.Object, deliveryOptionsMock.Object, authorizationServiceMock.Object, reactionStore, recorderMock.Object);
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
        SetupGetMandateForUser(addedMandate.Entity.Id, user.Entity, returnNull: true);

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
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
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count(), "The rejected declaration must not have created a delivery.");
    }

    [TestMethod]
    public async Task TheDatabaseRefusesASecondDeliveryForTheSameJob()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation(
            new Mandate { Name = TestHelpers.Localized(nameof(TheDatabaseRefusesASecondDeliveryForTheSameJob)), AllowDelivery = true });
        var jobId = Guid.NewGuid();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeclaringUser = user });
        await context.SaveChangesAsync();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeclaringUser = user });

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

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeclaringUser = user, Deleted = true });
        await context.SaveChangesAsync();

        context.Deliveries.Add(new Delivery { JobId = jobId, Mandate = mandate, DeclaringUser = user });
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
        var precursorDelivery = new Delivery() { JobId = Guid.NewGuid(), Mandate = mandate, DeclaringUser = user };
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
        var precursorDelivery = context.Deliveries.Add(new Delivery() { JobId = Guid.NewGuid(), Mandate = otherMandate, DeclaringUser = user }).Entity;
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
    }

    private void SetupJobPersistence(Guid jobId)
    {
        assetHandlerMock
            .Setup(p => p.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Asset> { new Asset(), new Asset() });
    }

    private Guid SetupProcessingJob(int? mandateId = null, ProcessingState jobState = ProcessingState.Success)
    {
        var guid = Guid.NewGuid();

        // The pipeline always reports Success, so a refused declaration shows that the gate reads the job state:
        // the job only becomes deliverable once its delivery files are staged.
        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.State).Returns(ProcessingState.Success);
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        pipelineMock.SetupGet(p => p.DisplayName).Returns(LocalizedText.Empty);

        var job = new ProcessingJob(guid, Guid.NewGuid(), mandateId, DateTime.Now)
        {
            Pipeline = pipelineMock.Object,
            State = jobState,
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
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeclaringUser = user };
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
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeclaringUser = context.Users.First(u => !u.IsAdmin) };
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
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeclaringUser = user };
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
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeclaringUser = uploader };
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
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeclaringUser = uploader, Date = DateTime.UtcNow.AddHours(-12) };
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
        var declaredAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        Assert.IsTrue(deliveryController.IsDeleteAllowedForUploader(declaredAt, new DateTime(2026, 1, 15, 22, 59, 0, DateTimeKind.Utc)));
        Assert.IsFalse(deliveryController.IsDeleteAllowedForUploader(declaredAt, new DateTime(2026, 1, 15, 23, 30, 0, DateTimeKind.Utc)));
    }

    [TestMethod]
    public void IsDeleteAllowedForUploaderStaysDisabledBeforeTheDeclaration()
    {
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = false });

        var declaredAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        Assert.IsFalse(deliveryController.IsDeleteAllowedForUploader(declaredAt, declaredAt.AddMinutes(-1)));
    }

    [TestMethod]
    public async Task Download()
    {
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
        assetHandlerMock.Setup(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Encoding.UTF8.GetBytes("Test"), "text/xml"));
        var delivery = CreateDeliveryWithAsset(context.Mandates.Single(m => m.StacAccess == StacAccess.Public));
        context.DeliveryReactions.AddRange(
            NewReaction(delivery, "drop", ReactionState.Done),
            NewReaction(delivery, "mail", ReactionState.Pending, ReactionKind.Notification));
        context.SaveChanges();

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as FileContentResult;

        Assert.AreEqual("Test.xml", result?.FileDownloadName);
    }

    private Delivery CreateDeliveryWithAsset(Mandate mandate)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = mandate, DeclaringUser = context.Users.First() };
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
    public async Task GetAsAdminReturnsList()
    {
        var admin = context.Users.First(u => u.IsAdmin);
        deliveryController.SetupTestUser(admin);

        var response = (await deliveryController.Get()) as ObjectResult;
        var list = response?.Value as List<DeliveryResponse>;

        Assert.IsNotNull(list);
        Assert.HasCount(context.Deliveries.Count(), list);
    }

    [TestMethod]
    public async Task GetListsTheReactionsOfEachDeliveryWithTheStateOfItsForwarding()
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var declarer = context.Users.First(u => !u.IsAdmin);
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        var forwarded = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeclaringUser = declarer, Comment = "weitergegeben" };
        var failed = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeclaringClient = client };
        var withoutReactions = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeclaringUser = declarer };
        context.Deliveries.AddRange(forwarded, failed, withoutReactions);
        context.DeliveryReactions.AddRange(
            NewReaction(forwarded, "ablage", ReactionState.Done),
            NewReaction(forwarded, "meldung", ReactionState.Pending, ReactionKind.Notification),
            NewReaction(failed, "ablage", ReactionState.Error),
            NewReaction(failed, "archiv", ReactionState.Pending));
        context.SaveChanges();

        var list = ((await deliveryController.Get()) as ObjectResult)?.Value as List<DeliveryResponse>;

        Assert.IsNotNull(list);
        var byId = list.ToDictionary(d => d.Id);
        var forwardedResponse = byId[forwarded.Id];
        Assert.AreEqual(declarer.FullName, forwardedResponse.DeclarerName);
        Assert.AreEqual(forwarded.Mandate.Id, forwardedResponse.Mandate.Id);
        Assert.AreEqual("weitergegeben", forwardedResponse.Comment);
        Assert.AreEqual(ReactionState.Done, forwardedResponse.ForwardingState, "a notification does not hold up the forwarding.");
        CollectionAssert.AreEqual(new[] { "ablage", "meldung" }, forwardedResponse.Reactions.Select(r => r.ReactionId).ToArray());
        Assert.IsNull(forwardedResponse.Reactions[0].NextAttemptAt, "a reaction that is done has no next attempt.");
        Assert.IsNotNull(forwardedResponse.Reactions[1].NextAttemptAt);
        Assert.AreEqual("SILENTHARBOR", byId[failed.Id].DeclarerName);
        Assert.AreEqual(ReactionState.Error, byId[failed.Id].ForwardingState);
        Assert.IsNull(byId[withoutReactions.Id].ForwardingState);
        Assert.IsEmpty(byId[withoutReactions.Id].Reactions);
    }

    [TestMethod]
    public async Task ResetReactionsMakesTheFailedReactionsDueAgain()
    {
        var admin = context.Users.First(u => u.IsAdmin);
        deliveryController.SetupTestUser(admin);
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Error), ("archiv", ReactionState.Done));

        Assert.IsInstanceOfType<NoContentResult>(await deliveryController.ResetReactions(delivery.Id));

        var states = context.DeliveryReactions
            .AsNoTracking()
            .Where(r => r.DeliveryId == delivery.Id)
            .ToDictionary(r => r.ReactionId, r => r.State);
        Assert.AreEqual(ReactionState.Pending, states["ablage"]);
        Assert.AreEqual(ReactionState.Done, states["archiv"]);
        recorderMock.Verify(
            recorder => recorder.RecordReactionsResetAsync(
                delivery.Id,
                It.Is<IReadOnlyList<DeliveryReaction>>(reactions => reactions.Count == 1 && reactions[0].ReactionId == "ablage" && reactions[0].State == ReactionState.Pending),
                admin.Id),
            Times.Once);
    }

    [TestMethod]
    public async Task ResetReactionsWithoutAFailedReactionIsAConflict()
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Done));

        var result = await deliveryController.ResetReactions(delivery.Id) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status409Conflict, result?.StatusCode);
        recorderMock.Verify(recorder => recorder.RecordReactionsResetAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<DeliveryReaction>>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Unknown delivery")]
    [DataRow(true, DisplayName = "Deleted delivery")]
    public async Task ResetReactionsOfADeliveryThatDoesNotExistIsNotFound(bool deleted)
    {
        deliveryController.SetupTestUser(context.Users.First(u => u.IsAdmin));
        var delivery = AddDeliveryWithReactions(("ablage", ReactionState.Error));
        delivery.Deleted = deleted;
        context.SaveChanges();
        var deliveryId = deleted ? delivery.Id : context.Deliveries.IgnoreQueryFilters().Max(d => d.Id) + 1;

        var result = await deliveryController.ResetReactions(deliveryId) as ObjectResult;

        Assert.AreEqual(StatusCodes.Status404NotFound, result?.StatusCode);
        Assert.AreEqual(ReactionState.Error, context.DeliveryReactions.IgnoreQueryFilters().AsNoTracking().Single(r => r.DeliveryId == delivery.Id).State);
        recorderMock.Verify(recorder => recorder.RecordReactionsResetAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<DeliveryReaction>>(), It.IsAny<int>()), Times.Never);
    }

    private Delivery AddDeliveryWithReactions(params (string ReactionId, ReactionState State)[] reactions)
    {
        var delivery = new Delivery { JobId = Guid.NewGuid(), Mandate = context.Mandates.First(), DeclaringUser = context.Users.First(u => !u.IsAdmin) };
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
    public async Task GetUploadsReturnsDeliveriesOfUserInTheOrderTheyWereDeclared()
    {
        var user = context.Users.First(u => !u.IsAdmin && context.Deliveries.Count(d => d.DeclaringUserId == u.Id) >= 2);
        deliveryController.SetupTestUser(user);

        // An update moves the row within the table, so the database no longer returns the deliveries in id order by chance.
        var first = context.Deliveries.Where(d => d.DeclaringUserId == user.Id).OrderBy(d => d.Id).First();
        first.Comment += " (edited)";
        context.SaveChanges();

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var uploads = Assert.IsInstanceOfType<List<UploadedDelivery>>(response?.Value);
        var expected = context.Deliveries
            .Include(d => d.Mandate)
            .Where(d => d.DeclaringUserId == user.Id)
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

    private User UserWithDeliveries()
        => context.Users.First(user => !user.IsAdmin && context.Deliveries.Any(delivery => delivery.DeclaringUserId == user.Id));

    private static DeliveryReaction NewReaction(Delivery delivery, string reactionId, ReactionState state, ReactionKind kind = ReactionKind.Transfer) => new()
    {
        Delivery = delivery,
        ReactionId = reactionId,
        Kind = kind,
        Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
        State = state,
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
        SetupGetMandateForUser(createdMandate.Id, user);
        return (user, createdMandate);
    }

    private void SetupGetMandateForUser(int mandateId, User user, bool returnNull = false)
    {
        // return a mandate loaded with AsNoTracking, like the MandateService
        var detachedMandate = returnNull
            ? null
            : context.Mandates.AsNoTracking().First(m => m.Id == mandateId);

        mandateServiceMock
            .Setup(s => s.GetMandateForDeliveryAsync(mandateId, Declarer.ForUser(user.Id)))
            .ReturnsAsync(detachedMandate);
    }
}
