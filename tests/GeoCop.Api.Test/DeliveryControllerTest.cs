using GeoCop.Api.Contracts;
using GeoCop.Api.Controllers;
using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.Test
{
    [TestClass]
    public class DeliveryControllerTest
    {
        private Mock<IValidatorService> validatorServiceMock;
        private Mock<ILogger<DeliveryController>> loggerMock;
        private DeliveryController deliveryController;
        private Context context;

        [TestInitialize]
        public void Setup()
        {
            loggerMock = new Mock<ILogger<DeliveryController>>();
            validatorServiceMock = new Mock<IValidatorService>();
            context = Initialize.DbFixture.GetTestContext();
            deliveryController = new DeliveryController(loggerMock.Object, context, validatorServiceMock.Object);
        }

        public void Cleanup()
        {
            validatorServiceMock.VerifyAll();
            context.Dispose();
        }

        [TestMethod]
        [DataRow(Status.Enqueued, StatusCodes.Status400BadRequest)]
        [DataRow(Status.Processing, StatusCodes.Status400BadRequest)]
        [DataRow(Status.CompletedWithErrors, StatusCodes.Status400BadRequest)]
        [DataRow(Status.Failed, StatusCodes.Status400BadRequest)]
        public void CreateFailsJobNotCompleted(Status status, int resultCode)
        {
            var guid = Guid.NewGuid();
            validatorServiceMock
                .Setup(s => s.GetJobStatusOrDefault(guid))
                .Returns((status, "Test"));

            var deliveriesCount = context.Deliveries.Count();
            var mandateId = context.DeliveryMandates.First().Id;

            var result = deliveryController.Create(new DeliveryRequest { JobId = guid,  DeliveryMandateId = mandateId }) as ObjectResult;

            context.ChangeTracker.Clear();

            Assert.IsNotNull(result);
            Assert.AreEqual(result.StatusCode, resultCode);
            Assert.AreEqual(deliveriesCount, context.Deliveries.Count());
        }

        [TestMethod]
        public void CreateFailsJobNotFound()
        {
            var guid = Guid.NewGuid();
            validatorServiceMock
                .Setup(s => s.GetJobStatusOrDefault(guid))
                .Returns(default((Status, string)));

            var deliveriesCount = context.Deliveries.Count();
            var mandateId = context.DeliveryMandates.First().Id;

            var result = deliveryController.Create(new DeliveryRequest { JobId = guid, DeliveryMandateId = mandateId }) as ObjectResult;

            context.ChangeTracker.Clear();

            Assert.IsNotNull(result);
            Assert.AreEqual(result.StatusCode, StatusCodes.Status404NotFound);
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
            Assert.AreEqual(result.StatusCode, StatusCodes.Status404NotFound);
        }

        [TestMethod]
        public void Create()
        {
            var guid = Guid.NewGuid();
            validatorServiceMock
                .Setup(s => s.GetJobStatusOrDefault(guid))
                .Returns((Status.Completed, "Test"));
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
}
