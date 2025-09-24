using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;
using System.Text;

namespace Geopilot.Api.Controllers;

[TestClass]
public class DeliveryControllerTest
{
    private Mock<IValidationService> validationServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private Mock<ILogger<DeliveryController>> loggerMock;
    private DeliveryController deliveryController;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<DeliveryController>>();
        validationServiceMock = new Mock<IValidationService>();
        assetHandlerMock = new Mock<IAssetHandler>();
        context = AssemblyInitialize.DbFixture.GetTestContext();
        deliveryController = new DeliveryController(loggerMock.Object, context, validationServiceMock.Object, assetHandlerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        validationServiceMock.VerifyAll();
        loggerMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(Status.Processing, StatusCodes.Status400BadRequest)]
    [DataRow(Status.CompletedWithErrors, StatusCodes.Status400BadRequest)]
    [DataRow(Status.Failed, StatusCodes.Status400BadRequest)]
    public async Task CreateFailsJobNotCompleted(Status status, int resultCode)
    {
        var guid = SetupValidationJob(status);
        var deliveriesCount = context.Deliveries.Count();
        var mandateId = context.Mandates.First().Id;

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid,  MandateId = mandateId })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(resultCode, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
    }

    [TestMethod]
    public async Task CreateFailsJobNotFound()
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(default(ValidationJob?));

        var deliveriesCount = context.Deliveries.Count();
        var mandateId = context.Mandates.First().Id;

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid, MandateId = mandateId })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
    }

    [TestMethod]
    public async Task CreateFailsUnauthorizedUser()
    {
        var guid = SetupValidationJob();

        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
        var addedMandate = context.Mandates.Add(new Mandate());
        context.SaveChanges();

        deliveryController.SetupTestUser(user.Entity);
        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid, MandateId = addedMandate.Entity.Id })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateMinimalDelivery()
    {
        var startTime = DateTime.Now;
        Guid jobId = SetupValidationJob();
        SetupJobPersistence(jobId);
        var (user, mandate) = AddMandateForAuthorizedUser(
            new Mandate
            {
                Name = nameof(CreateMinimalDelivery),
                EvaluateComment = FieldEvaluationType.NotEvaluated,
                EvaluatePartial = FieldEvaluationType.NotEvaluated,
                EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
            });

        var request = new DeliveryRequest
        {
            JobId = jobId,
            MandateId = mandate.Id,
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
        Assert.AreEqual(null, returnedDelivery.Partial);
        Assert.AreEqual(null, returnedDelivery.PrecursorDelivery);

        var dbDelivery = context.Deliveries
            .Include(d => d.Mandate)
            .FirstOrDefault(d => d.Id == returnedDelivery.Id);

        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(DateTimeKind.Utc, dbDelivery.Date.Kind);
        Assert.IsTrue(dbDelivery.Date > startTime.ToUniversalTime() && dbDelivery.Date < DateTime.UtcNow);
        Assert.AreEqual(jobId, dbDelivery.JobId);
        Assert.AreEqual(mandate.Id, dbDelivery.Mandate.Id);
        Assert.AreEqual(string.Empty, dbDelivery.Comment);
        Assert.AreEqual(null, dbDelivery.Partial);
        Assert.AreEqual(null, dbDelivery.PrecursorDelivery);
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
        Guid jobId = SetupValidationJob();
        SetupJobPersistence(jobId);
        var (user, mandate) = AddMandateForAuthorizedUser(
            new Mandate
            {
                Name = nameof(CreateValidatesComment),
                EvaluateComment = evaluaton,
            });

        var request = new DeliveryRequest
        {
            JobId = jobId,
            MandateId = mandate.Id,
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
    public async Task CreateValidatesPartalDelivery(FieldEvaluationType evaluaton, bool? partialDelivery, Type responseValueType, bool? dbValue)
    {
        Guid jobId = SetupValidationJob();
        SetupJobPersistence(jobId);
        var (user, mandate) = AddMandateForAuthorizedUser(
            new Mandate
            {
                Name = nameof(CreateValidatesComment),
                EvaluatePartial = evaluaton,
            });

        var request = new DeliveryRequest
        {
            JobId = jobId,
            MandateId = mandate.Id,
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
        Guid jobId = SetupValidationJob();
        if (responseValueType == typeof(Delivery))
            SetupJobPersistence(jobId);

        var (user, mandate) = AddMandateForAuthorizedUser(
            new Mandate
            {
                Name = nameof(CreateValidatesComment),
                EvaluatePrecursorDelivery = evaluaton,
            });
        var precursorDelivery = new Delivery() { JobId = Guid.NewGuid(), Mandate = mandate, DeclaringUser = user };
        context.Deliveries.Add(precursorDelivery);
        context.SaveChanges();

        int? precursorId = setPrecursor ? precursorDelivery.Id : null;

        var request = new DeliveryRequest
        {
            JobId = jobId,
            MandateId = mandate.Id,
            PrecursorDeliveryId = precursorId,
        };

        var response = await deliveryController.Create(request);
        Assert.IsNotNull(response);
        AssertResponseValueType(responseValueType, response);
    }

    [TestMethod]
    public async Task CreateFailsMandateNotFound()
    {
        var guid = SetupValidationJob();
        var (user, mandate) = AddMandateForAuthorizedUser(new Mandate { Name = nameof(CreateFailsMandateNotFound) });
        var nextMandateId = context.Mandates.Max(m => m.Id) + 1;
        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid, MandateId = nextMandateId })) as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public async Task CreateFailsPrecursorFromOtherMandate()
    {
        var guid = SetupValidationJob();
        var deliveriesCount = context.Deliveries.Count();
        var (user, mandate) = AddMandateForAuthorizedUser(new Mandate { Name = nameof(CreateFailsPrecursorFromOtherMandate) });
        mandate.EvaluatePrecursorDelivery = FieldEvaluationType.Required;
        var otherMandate = context.Mandates.Add(new Mandate { Name = nameof(CreateFailsPrecursorFromOtherMandate) }).Entity;
        var precursorDelivery = new Delivery() { JobId = Guid.NewGuid(), Mandate = otherMandate, DeclaringUser = user };
        context.SaveChanges();

        var request = new DeliveryRequest
        {
            JobId = guid,
            MandateId = mandate.Id,
            PrecursorDeliveryId = precursorDelivery.Id,
        };

        var result = await deliveryController.Create(request);
        Assert.IsNotNull(result);
        AssertResponseValueType(typeof(ValidationProblemDetails), result);
    }

    [TestMethod]
    public async Task CreateFailsPrecursorNotFound()
    {
        var guid = SetupValidationJob();
        var deliveriesCount = context.Deliveries.Count();
        var (user, mandate) = AddMandateForAuthorizedUser(new Mandate { Name = nameof(CreateFailsPrecursorNotFound) });
        mandate.EvaluatePrecursorDelivery = FieldEvaluationType.Required;
        var unknownDeliveryId = context.Deliveries.Max(d => d.Id) + 1;

        var request = new DeliveryRequest
        {
            JobId = guid,
            MandateId = mandate.Id,
            PrecursorDeliveryId = unknownDeliveryId,
        };
        var result = await deliveryController.Create(request);
        Assert.IsNotNull(result);
        AssertResponseValueType(typeof(ValidationProblemDetails), result);
    }

    private (User User, Mandate Mandate) AddMandateForAuthorizedUser(Mandate mandate)
    {
        var user = new User() { FullName = nameof(DeliveryControllerTest) };
        var organisation = new Organisation() { Name = nameof(DeliveryControllerTest) };

        mandate.Organisations.Add(organisation);
        organisation.Users.Add(user);
        context.Mandates.Add(mandate);
        context.SaveChanges();

        deliveryController.SetupTestUser(user);
        return (user, mandate);
    }

    private void SetupJobPersistence(Guid jobId)
    {
        assetHandlerMock
            .Setup(p => p.PersistJobAssets(jobId))
            .Returns(new List<Asset> { new Asset(), new Asset() });
    }

    private Guid SetupValidationJob(Status jobStatus = Status.Completed)
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(new ValidationJob(guid, "ORIGINAL.zip", "TEMP.zip", ImmutableDictionary<string, ValidatorResult?>.Empty, jobStatus));
        return guid;
    }

    [TestMethod]
    public void Delete()
    {
        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid, Mandate = context.Mandates.First(), DeclaringUser = context.Users.First() };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        var result = deliveryController.Delete(delivery.Id) as OkResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);

        var dbDelivery = context.DeliveriesWithIncludes
            .IgnoreQueryFilters()
            .FirstOrDefault(d => d.Id == delivery.Id);
        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(true, dbDelivery.Deleted);
        Assert.AreEqual(true, dbDelivery.Assets.All(a => a.Deleted));
    }

    [TestMethod]
    public void DeleteFailsDeliveryNotFound()
    {
        var result = deliveryController.Delete(context.Deliveries.Max(d => d.Id) + 1) as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
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
        Assert.AreEqual(context.Deliveries.Count(), list.Count);
    }

    [TestMethod]
    public async Task GetAsUserReturnsListFilteredByOrganisations()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);

        var response = (await deliveryController.Get()) as ObjectResult;
        var list = response?.Value as List<Delivery>;

        var accessibleDeliveries = context.Users
            .Include(u => u.Organisations)
            .ThenInclude(o => o.Mandates)
            .ThenInclude(m => m.Deliveries)
            .First(u => u.Id == user.Id)
            .Organisations
            .SelectMany(o => o.Mandates)
            .SelectMany(m => m.Deliveries)
            .ToList();

        Assert.IsNotNull(list);
        Assert.AreNotEqual(0, accessibleDeliveries.Count);
        Assert.AreEqual(accessibleDeliveries.Count, list.Count);
        CollectionAssert.AllItemsAreUnique(list);
    }

    [TestMethod]
    public async Task GetAsAdminReturnsListFilteredByMandateId()
    {
        var admin = context.Users.First(u => u.IsAdmin);
        admin.Organisations.Clear();
        context.SaveChanges();
        deliveryController.SetupTestUser(admin);
        var mandateId = context.Mandates
            .Where(m => m.Deliveries.Count != 0)
            .First()
            .Id;

        var response = (await deliveryController.Get(mandateId)) as ObjectResult;
        var list = response?.Value as List<Delivery>;

        Assert.IsNotNull(list);
        Assert.AreEqual(context.Deliveries.Where(d => d.Mandate != null && d.Mandate.Id == mandateId).Count(), list.Count);
    }

    [TestMethod]
    public async Task GetAsUserReturnsNotFoundForUnauthorizedMandate()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);
        var mandateId = context.Mandates
            .Where(m => !m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id))
            .First()
            .Id;

        var response = await deliveryController.Get(mandateId);

        Assert.IsInstanceOfType(response, typeof(NotFoundResult));
    }

    [TestMethod]
    public async Task GetAsUserReturnsListFilteredByOrganisationsAndMandateId()
    {
        var user = context.Users.First(u => !u.IsAdmin);
        deliveryController.SetupTestUser(user);
        var mandateId = context.Mandates
            .Where(m => m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id) && m.Deliveries.Count != 0)
            .First()
            .Id;

        var response = (await deliveryController.Get(mandateId)) as ObjectResult;
        var list = response?.Value as List<Delivery>;

        var deliveris = context.Mandates
            .Include(m => m.Deliveries)
            .First(m => m.Id == mandateId)
            .Deliveries;

        Assert.IsNotNull(list);
        Assert.AreNotEqual(0, deliveris.Count);
        Assert.AreEqual(deliveris.Count, list.Count);
        CollectionAssert.AllItemsAreUnique(list);
    }
}
