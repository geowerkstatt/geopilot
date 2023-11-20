using GeoCop.Api.Contracts;
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
    private Mock<ILogger<DeliveryController>> loggerMock;
    private DeliveryController deliveryController;
    private Context context;

    [TestInitialize]
    public void Setup()
    {
        loggerMock = new Mock<ILogger<DeliveryController>>();
        validationServiceMock = new Mock<IValidationService>();
        context = Initialize.DbFixture.GetTestContext();
        deliveryController = new DeliveryController(loggerMock.Object, context, validationServiceMock.Object);
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
            .Setup(s => s.GetJobStatus(guid))
            .Returns(new ValidationJobStatus(guid) { Status = Status.Completed });
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
        Assert.AreEqual(DateTimeKind.Utc, dbDelivery.Date.Kind);
        Assert.IsTrue(dbDelivery.Date > startTime.ToUniversalTime() && dbDelivery.Date < DateTime.UtcNow);
    }
}
