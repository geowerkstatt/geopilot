using GeoCop.Api.Controllers;
using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.Test.Controllers
{
    [TestClass]
    public class MandateControllerTest
    {
        private Mock<ILogger<MandateController>> loggerMock;
        private Mock<IValidationService> validationServiceMock;
        private Context context;
        private MandateController mandateController;
        private User user;
        private DeliveryMandate unrestricedMandate;
        private DeliveryMandate xtfMandate;
        private DeliveryMandate unassociatedMandate;

        [TestInitialize]
        public void Setup()
        {
            loggerMock = new Mock<ILogger<MandateController>>();
            validationServiceMock = new Mock<IValidationService>();
            context = Initialize.DbFixture.GetTestContext();
            mandateController = new MandateController(loggerMock.Object, context, validationServiceMock.Object);

            unrestricedMandate = new DeliveryMandate() { FileTypes = new string[] { ".*" }, Name = nameof(unrestricedMandate) };
            xtfMandate = new DeliveryMandate() { FileTypes = new string[] { ".xtf" }, Name = nameof(xtfMandate) };
            unassociatedMandate = new DeliveryMandate { FileTypes = new string[] { "*.itf" }, Name = nameof(unassociatedMandate) };

            context.DeliveryMandates.Add(unrestricedMandate);
            context.DeliveryMandates.Add(xtfMandate);
            context.DeliveryMandates.Add(unassociatedMandate);

            user = new User { AuthIdentifier = "123" };
            context.Users.Add(user);

            var tempOrg = new Organisation { Name = "TestOrg" };
            tempOrg.Mandates.Add(unrestricedMandate);
            tempOrg.Mandates.Add(xtfMandate);
            tempOrg.Users.Add(user);

            context.Add(tempOrg);
            context.SaveChanges();
        }

        [TestMethod]
        public async Task GetReturnsListOfMandatesForUser()
        {
            mandateController.SetupTestUser(user);

            var result = (await mandateController.Get()) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<DeliveryMandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.Contains(mandates, unrestricedMandate);
            CollectionAssert.Contains(mandates, xtfMandate);
            CollectionAssert.DoesNotContain(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdIncludesMatchingMandates()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(user);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf"));

            var result = (await mandateController.Get(jobId.ToString())) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<DeliveryMandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.Contains(mandates, unrestricedMandate);
            CollectionAssert.Contains(mandates, xtfMandate);
            CollectionAssert.DoesNotContain(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdExcludesNonMatchinMandates()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(user);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.csv", "tmp.csv"));

            var result = (await mandateController.Get(jobId: jobId.ToString())) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<DeliveryMandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.Contains(mandates, unrestricedMandate);
            CollectionAssert.DoesNotContain(mandates, xtfMandate);
            CollectionAssert.DoesNotContain(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithInvalidJobIdReturnsEmptyArray()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(user);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(() => null);

            var result = (await mandateController.Get(jobId: jobId.ToString())) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<DeliveryMandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.AreEquivalent(Array.Empty<DeliveryMandate>(), mandates);
        }

    }
}
