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
using System.Text;

namespace GeoCop.Api.Controllers;

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

    public void Cleanup()
    {
        validationServiceMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(Status.Processing, StatusCodes.Status400BadRequest)]
    [DataRow(Status.CompletedWithErrors, StatusCodes.Status400BadRequest)]
    [DataRow(Status.Failed, StatusCodes.Status400BadRequest)]
    public async Task CreateFailsJobNotCompleted(Status status, int resultCode)
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

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid,  DeliveryMandateId = mandateId })) as ObjectResult;

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
        var mandateId = context.DeliveryMandates.First().Id;

        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid, DeliveryMandateId = mandateId })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
    }

    [TestMethod]
    public async Task CreateFailsUnauthorizedUser()
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJobStatus(guid))
            .Returns(new ValidationJobStatus(guid) { Status = Status.Completed });

        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() });
        var addedMandate = context.DeliveryMandates.Add(new DeliveryMandate());
        context.SaveChanges();

        deliveryController.SetupTestUser(user.Entity);
        var result = (await deliveryController.Create(new DeliveryRequest { JobId = guid, DeliveryMandateId = addedMandate.Entity.Id })) as ObjectResult;

        context.ChangeTracker.Clear();

        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Create(bool setOptionals)
    {
        var guid = Guid.NewGuid();
        validationServiceMock
            .Setup(s => s.GetJob(guid))
            .Returns(new ValidationJob(guid, "OriginalName", "TempFileName"));
        validationServiceMock
            .Setup(s => s.GetJobStatus(guid))
            .Returns(new ValidationJobStatus(guid) { JobId = guid, Status = Status.Completed });
        assetHandlerMock
            .Setup(p => p.PersistJobAssets(guid))
            .Returns(new List<Asset> { new Asset(), new Asset() });
        var startTime = DateTime.Now;

        var testUserWithMandate = context.Users
            .Include(u => u.Organisations)
            .ThenInclude(o => o.Mandates)
            .ThenInclude(m => m.Deliveries)
            .First(u => u.Organisations.SelectMany(o => o.Mandates).Any());

        deliveryController.SetupTestUser(testUserWithMandate);

        var mandate = testUserWithMandate.Organisations.First().Mandates.First();
        var mandateId = mandate.Id;
        var predecessorDeliveryId = mandate.Deliveries.Last().Id;

        var request = new DeliveryRequest
        {
            JobId = guid,
            DeliveryMandateId = mandateId,
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
            .Include(d => d.DeliveryMandate)
            .Include(d => d.PrecursorDelivery)
            .FirstOrDefault(d => d.Id == delivery.Id);

        Assert.IsNotNull(dbDelivery);
        Assert.AreEqual(DateTimeKind.Utc, dbDelivery.Date.Kind);
        Assert.IsTrue(dbDelivery.Date > startTime.ToUniversalTime() && dbDelivery.Date < DateTime.UtcNow);
        Assert.AreEqual(guid, dbDelivery.JobId);
        Assert.AreEqual(request.DeliveryMandateId, dbDelivery.DeliveryMandate.Id);
        Assert.AreEqual(request.Comment?.Trim() ?? string.Empty, dbDelivery.Comment);
        Assert.AreEqual(request.PartialDelivery, dbDelivery.Partial);
        Assert.AreEqual(request.PrecursorDeliveryId, dbDelivery.PrecursorDelivery?.Id);
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
        var delivery = new Delivery { JobId = guid };
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
}
