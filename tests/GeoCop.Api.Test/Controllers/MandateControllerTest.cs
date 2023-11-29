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
        private DeliveryMandate unrestrictedMandate;
        private DeliveryMandate xtfMandate;
        private DeliveryMandate unassociatedMandate;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<MandateController>>();
            validationServiceMock = new Mock<IValidationService>();
            context = AssemblyInitialize.DbFixture.GetTestContext();
            mandateController = new MandateController(loggerMock.Object, context, validationServiceMock.Object);

            unrestrictedMandate = new DeliveryMandate { FileTypes = new string[] { ".*" }, Name = nameof(unrestrictedMandate) };
            xtfMandate = new DeliveryMandate { FileTypes = new string[] { ".xtf" }, Name = nameof(xtfMandate) };
            unassociatedMandate = new DeliveryMandate { FileTypes = new string[] { "*.itf" }, Name = nameof(unassociatedMandate) };

            context.DeliveryMandates.Add(unrestrictedMandate);
            context.DeliveryMandates.Add(xtfMandate);
            context.DeliveryMandates.Add(unassociatedMandate);

            user = new User { AuthIdentifier = "123" };
            context.Users.Add(user);

            var tempOrg = new Organisation { Name = "TestOrg" };
            tempOrg.Mandates.Add(unrestrictedMandate);
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
            CollectionAssert.Contains(mandates, unrestrictedMandate);
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
            CollectionAssert.Contains(mandates, unrestrictedMandate);
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
            CollectionAssert.Contains(mandates, unrestrictedMandate);
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

        [TestMethod]
        public async Task GetWithoutValidDbUserReturnUnauthorized()
        {
            mandateController.SetupTestUser(new User { AuthIdentifier = "NotRegisteredUserId" });

            var result = await mandateController.Get();

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
            loggerMock.VerifyAll();
            validationServiceMock.VerifyAll();
        }
    }
}
