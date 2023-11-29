using GeoCop.Api.Contracts;
using GeoCop.Api.FileAccess;
using GeoCop.Api.Models;
using GeoCop.Api.Test;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.Controllers;

[TestClass]
public class DeliveryControllerTest
{
    private Mock<IValidationService> validationServiceMock;
    private Mock<IValidationAssetPersistor> validationAssetPersistorMock;
    private Mock<IPersistedAssetDeleter> persistedAssetDeleterMock;
    private Mock<ILogger<DeliveryController>> loggerMock;
    private DeliveryController deliveryController;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<DeliveryController>>();
        validationServiceMock = new Mock<IValidationService>();
        validationAssetPersistorMock = new Mock<IValidationAssetPersistor>();
        persistedAssetDeleterMock = new Mock<IPersistedAssetDeleter>();
        context = AssemblyInitialize.DbFixture.GetTestContext();
        deliveryController = new DeliveryController(loggerMock.Object, context, validationServiceMock.Object, validationAssetPersistorMock.Object, persistedAssetDeleterMock.Object);
    }

    public void Cleanup()
    {
        validationServiceMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(Status.Processing, StatusCodes.Status400BadRequest)]
    [DataRow(Status.CompletedWithErrors, StatusCodes.Status400BadRequest)]
    [DataRow(Status.Failed, StatusCodes.Status400BadRequest)]
    public void CreateFailsJobNotCompleted(Status status, int resultCode)
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(new ValidationJob(guid, "OriginalName", "TempFileName"));
        validationServiceMock
            .Setup(s => s.GetJobStatus(guid))
            .Returns(new ValidationJobStatus(guid) { Status = status });

        var deliveriesCount = context.Deliveries.Count();
        var mandateId = context.DeliveryMandates.First().Id;

        var result = deliveryController.Create(new DeliveryRequest { JobId = guid,  DeliveryMandateId = mandateId }) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(resultCode, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
    }

    [TestMethod]
    public void CreateFailsJobNotFound()
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJobStatus(guid))
            .Returns(default(ValidationJobStatus?));

        var deliveriesCount = context.Deliveries.Count();
        var mandateId = context.DeliveryMandates.First().Id;

        var result = deliveryController.Create(new DeliveryRequest { JobId = guid, DeliveryMandateId = mandateId }) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
    }

    [TestMethod]
    public void CreateFailsUnauthorizedUser()
    {
        var guid = Guid.NewGuid();

        var addedMandate = context.DeliveryMandates.Add(new DeliveryMandate());
        context.SaveChanges();

        var result = deliveryController.Create(new DeliveryRequest { JobId = guid, DeliveryMandateId = addedMandate.Entity.Id }) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public void Create()
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(new ValidationJob(guid, "OriginalName", "TempFileName"));
        validationServiceMock
            .Setup(s => s.GetJobStatus(guid))
            .Returns(new ValidationJobStatus(guid) { JobId = guid, Status = Status.Completed });
        validationAssetPersistorMock
            .Setup(p => p.PersistJobAssets(guid))
            .Returns(new List<Asset> { new Asset(), new Asset() });
        var startTime = DateTime.Now;

        var dummyUserWithMandates = context.Users
            .Include(u => u.Organisations)
            .ThenInclude(o => o.Mandates)
            .First(u => u.Organisations.SelectMany(o => o.Mandates).Any());

        var mandateId = dummyUserWithMandates.Organisations.First().Mandates.First().Id;

        var result = deliveryController.Create(new DeliveryRequest { JobId = guid, DeliveryMandateId = mandateId }) as ObjectResult;

        context.ChangeTracker.Clear();
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);

        var delivery = result.Value as Delivery;
        Assert.IsNotNull(delivery);

        var dbDelivery = context.Deliveries
            .FirstOrDefault(d => d.Id == delivery.Id);

        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(guid, dbDelivery.JobId);
        Assert.AreEqual(DateTimeKind.Utc, dbDelivery.Date.Kind);
        Assert.IsTrue(dbDelivery.Date > startTime.ToUniversalTime() && dbDelivery.Date < DateTime.UtcNow);
    }

    [TestMethod]
    public void Delete()
    {
        var guid = Guid.NewGuid();
        var delivery = new Delivery { JobId = guid };
        delivery.Assets.Add(new Asset());
        context.Deliveries.Add(delivery);
        context.SaveChanges();

        var result = deliveryController.Delete(delivery.Id) as OkResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);

        var dbDelivery = context.Deliveries.Include(d => d.Assets).FirstOrDefault(d => d.Id == delivery.Id);
        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(true, dbDelivery.Deleted);
        Assert.AreEqual(true, dbDelivery.Assets.All(a => a.Deleted));
    }

    [TestMethod]
    public void DeleteDeliveryNotFound()
    {
        context.Deliveries.Max(d => d.Id);
        var result = deliveryController.Delete(context.Deliveries.Max(d => d.Id) + 1) as ObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }
}
