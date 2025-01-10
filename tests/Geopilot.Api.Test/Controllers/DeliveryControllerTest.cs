using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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
            .Setup(s => s.GetJobStatus(guid))
            .Returns(default(ValidationJobStatus?));

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
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateHandlesMinimalDelivery(bool setOptionals)
    {
        Guid jobId = SetupValidationJob();
        assetHandlerMock
            .Setup(p => p.PersistJobAssets(jobId))
            .Returns(new List<Asset> { new Asset(), new Asset() });
        var startTime = DateTime.Now;

        var testUserWithMandate = context.Users
            .Include(u => u.Organisations)
            .ThenInclude(o => o.Mandates)
            .ThenInclude(m => m.Deliveries)
            .First(u => u.Organisations.SelectMany(o => o.Mandates).Any());

        deliveryController.SetupTestUser(testUserWithMandate);

        var mandate2 = new Mandate { EvaluateComment = FieldEvaluationType.NotEvaluated, };
        var mandate = testUserWithMandate.Organisations.First().Mandates.First();
        var mandateId = mandate.Id;
        var predecessorDeliveryId = mandate.Deliveries.Last().Id;

        var request = new DeliveryRequest
        {
            JobId = jobId,
            MandateId = mandateId,
            Comment = setOptionals ? "Some test comment   " : null,
            PartialDelivery = setOptionals,
            PrecursorDeliveryId = setOptionals ? predecessorDeliveryId : null,
        };

        var result = (await deliveryController.Create(request)) as ObjectResult;

        context.ChangeTracker.Clear();
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);

        var delivery = result.Value as Delivery;
        Assert.IsNotNull(delivery);

        var dbDelivery = context.Deliveries
            .Include(d => d.Mandate)
            .Include(d => d.PrecursorDelivery)
            .FirstOrDefault(d => d.Id == delivery.Id);

        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(DateTimeKind.Utc, dbDelivery.Date.Kind);
        Assert.IsTrue(dbDelivery.Date > startTime.ToUniversalTime() && dbDelivery.Date < DateTime.UtcNow);
        Assert.AreEqual(jobId, dbDelivery.JobId);
        Assert.AreEqual(request.MandateId, dbDelivery.Mandate?.Id);
        Assert.AreEqual(request.Comment?.Trim() ?? string.Empty, dbDelivery.Comment);
        Assert.AreEqual(request.PartialDelivery, dbDelivery.Partial);
        Assert.AreEqual(request.PrecursorDeliveryId, dbDelivery.PrecursorDelivery?.Id);
    }

    private Guid SetupValidationJob(Status jobStatus = Status.Completed)
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJobStatus(guid))
            .Returns(new ValidationJobStatus(guid) { JobId = guid, Status = jobStatus });
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
