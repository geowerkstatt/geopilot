using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Globalization;
using System.Text;

namespace Geopilot.Api.Controllers;

[TestClass]
public class DeliveryControllerTest
{
    private Mock<IProcessingService> processingServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private Mock<ILogger<DeliveryController>> loggerMock;
    private Mock<IOptions<DeliveryOptions>> deliveryOptionsMock;
    private DeliveryController deliveryController;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<DeliveryController>>();
        processingServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        assetHandlerMock = new Mock<IAssetHandler>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        deliveryOptionsMock = new Mock<IOptions<DeliveryOptions>>();
        deliveryOptionsMock.Setup(o => o.Value).Returns(new DeliveryOptions { UploaderDeleteEnabled = true });
        deliveryController = new DeliveryController(loggerMock.Object, context, processingServiceMock.Object, mandateServiceMock.Object, assetHandlerMock.Object, deliveryOptionsMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        processingServiceMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        assetHandlerMock.VerifyAll();
        loggerMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(ProcessingState.Running)]
    [DataRow(ProcessingState.Failed)]
    [DataRow(ProcessingState.Cancelled)]
    [DataRow(ProcessingState.DeliveryRestriction)]
    public async Task CreateFailsJobNotCompleted(ProcessingState pipelineState)
    {
        var mandateId = context.Mandates.First().Id;
        var guid = SetupProcessingJob(mandateId, pipelineState);
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
        SetupGetMandateForUser(addedMandate.Entity.Id, user.Entity, returnNull: !publicMandate);

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateWithPublicMandate()
    {
        // This test case should verify that a delivery can be created for a public mandate even if the user is not explicitly linked to it via an organisation
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
        var publicMandate = context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateWithPublicMandate)),
            IsPublic = true,
            AllowDelivery = true,
        });
        context.SaveChanges();
        deliveryController.SetupTestUser(user.Entity);
        var jobId = SetupProcessingJob(publicMandate.Entity.Id);
        SetupJobPersistence(jobId);
        SetupGetMandateForUser(publicMandate.Entity.Id, user.Entity);

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
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
        var publicMandate = context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateSucceedsWhenPipelineCompletedWithWarnings)),
            IsPublic = true,
            AllowDelivery = true,
        });
        context.SaveChanges();
        deliveryController.SetupTestUser(user.Entity);
        var jobId = SetupProcessingJob(publicMandate.Entity.Id, ProcessingState.Warning);
        SetupJobPersistence(jobId);
        SetupGetMandateForUser(publicMandate.Entity.Id, user.Entity);

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = jobId })) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateWithMandateThatDoesNotAllowDelivery()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
        var publicMandate = context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreateWithMandateThatDoesNotAllowDelivery)),
            IsPublic = true,
            AllowDelivery = false,
        });
        context.SaveChanges();
        deliveryController.SetupTestUser(user.Entity);
        var jobId = SetupProcessingJob(publicMandate.Entity.Id);
        SetupGetMandateForUser(publicMandate.Entity.Id, user.Entity);

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

    private Guid SetupProcessingJob(int? mandateId = null, ProcessingState pipelineState = ProcessingState.Success)
    {
        var guid = Guid.NewGuid();
        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.State).Returns(pipelineState);
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        pipelineMock.SetupGet(p => p.DisplayName).Returns(LocalizedText.Empty);

        var job = new ProcessingJob(guid, Guid.NewGuid(), mandateId, DateTime.Now)
        {
            Pipeline = pipelineMock.Object,
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

        var delivery = new Delivery { Date = deliveryDate };
        Assert.AreEqual(expected, deliveryController.IsDeleteAllowedForUploader(delivery, currentDate));
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

        var delivery = new Delivery { Date = deliveryDate };
        Assert.IsFalse(deliveryController.IsDeleteAllowedForUploader(delivery, restrictedDate));
        Assert.IsTrue(deliveryController.IsDeleteAllowedForUploader(delivery, allowedDate));
        Assert.AreEqual(deliveryDateTimeKind, delivery.Date.Kind);
    }

    [TestMethod]
    public async Task Download()
    {
        assetHandlerMock.Setup(p => p.DownloadAssetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Encoding.UTF8.GetBytes("Test"), "text/xml"));
        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeclaringUser = context.Users.First() };
        delivery.Assets.Add(new Asset() { OriginalFilename = "Test.xml", SanitizedFilename = "xyz.xml" });
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        var result = await deliveryController.DownloadAsync(delivery.Assets[0].Id) as FileContentResult;
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.FileContents);
        Assert.AreEqual("Test.xml", result.FileDownloadName);
        Assert.AreEqual("text/xml", result.ContentType);
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
        var list = response?.Value as List<Delivery>;

        Assert.IsNotNull(list);
        Assert.HasCount(context.Deliveries.Count(), list);
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
    public async Task GetUploadsReturnsDeliveriesOfUser()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);

        var response = (await deliveryController.GetUploads()) as ObjectResult;

        var deliveries = Assert.IsInstanceOfType<List<Delivery>>(response?.Value);
        CollectionAssert.AllItemsAreUnique(deliveries);
        Assert.IsTrue(deliveries.All(d => d.DeclaringUser.Id == user.Id), "All deliveries should belong to the user.");
        Assert.IsTrue(deliveries.All(d => !d.Deleted), "Should not return deleted deliveries.");
    }

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
            .Setup(s => s.GetMandateForUser(mandateId, user))
            .ReturnsAsync(detachedMandate);
    }
}
