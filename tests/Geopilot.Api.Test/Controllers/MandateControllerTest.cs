using Geopilot.Api.Controllers;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Controllers
{
    [TestClass]
    public class MandateControllerTest
    {
        private Mock<ILogger<Mandate>> loggerMock;
        private Mock<IValidationService> validationServiceMock;
        private Context context;
        private MandateController mandateController;
        private User editUser;
        private User adminUser;
        private Mandate unrestrictedMandate;
        private Mandate xtfMandate;
        private Mandate unassociatedMandate;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<Mandate>>();
            validationServiceMock = new Mock<IValidationService>();
            context = AssemblyInitialize.DbFixture.GetTestContext();
            mandateController = new MandateController(loggerMock.Object, context, validationServiceMock.Object);

            unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(unrestrictedMandate) };
            xtfMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = nameof(xtfMandate) };
            unassociatedMandate = new Mandate { FileTypes = new string[] { "*.itf" }, Name = nameof(unassociatedMandate) };

            context.Mandates.Add(unrestrictedMandate);
            context.Mandates.Add(xtfMandate);
            context.Mandates.Add(unassociatedMandate);

            editUser = new User { AuthIdentifier = "123", FullName = "Edit User" };
            context.Users.Add(editUser);
            adminUser = new User { AuthIdentifier = "1234", FullName = "Admin User", IsAdmin = true };
            context.Users.Add(adminUser);

            var tempOrg = new Organisation { Name = "TestOrg" };
            tempOrg.Mandates.Add(unrestrictedMandate);
            tempOrg.Mandates.Add(xtfMandate);
            tempOrg.Users.Add(editUser);
            tempOrg.Users.Add(adminUser);

            context.Add(tempOrg);
            context.SaveChanges();
        }

        [TestMethod]
        public async Task GetReturnsListOfMandatesForUser()
        {
            mandateController.SetupTestUser(editUser);

            var result = (await mandateController.Get()) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.Contains(mandates, unrestrictedMandate);
            CollectionAssert.Contains(mandates, xtfMandate);
            CollectionAssert.DoesNotContain(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdIncludesMatchingMandates()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(editUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf"));

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.Contains(mandates, unrestrictedMandate);
            CollectionAssert.Contains(mandates, xtfMandate);
            CollectionAssert.DoesNotContain(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdExcludesNonMatchinMandates()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(editUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.csv", "tmp.csv"));

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.Contains(mandates, unrestrictedMandate);
            CollectionAssert.DoesNotContain(mandates, xtfMandate);
            CollectionAssert.DoesNotContain(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithInvalidJobIdReturnsEmptyArray()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(editUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(() => null);

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            CollectionAssert.AreEquivalent(Array.Empty<Mandate>(), mandates);
        }

        [TestMethod]
        public async Task GetWithoutValidDbUserReturnUnauthorized()
        {
            mandateController.SetupTestUser(new User { AuthIdentifier = "NotRegisteredUserId" });

            var result = await mandateController.Get();

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        }

        [TestMethod]
        public async Task CreateMandate()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate() { FileTypes = new string[] { ".*" }, Name = "Test create" };
            var result = await mandateController.Create(mandate).ConfigureAwait(false);
            ActionResultAssert.IsCreated(result);
        }

        [TestMethod]
        public async Task CreateMandateUnauthorized()
        {
            mandateController.SetupTestUser(editUser);
            var mandate = new Mandate() { FileTypes = new string[] { ".*" }, Name = "Test create" };
            var result = await mandateController.Create(mandate).ConfigureAwait(false);
            ActionResultAssert.IsUnauthorized(result);
        }

        [TestMethod]
        public async Task EditMandate()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate() { FileTypes = new string[] { ".*" }, Name = "Test update" };
            var result = await mandateController.Create(mandate).ConfigureAwait(false) as CreatedResult;

            mandateController.SetupTestUser(adminUser);
            var updatedMandate = result?.Value as Mandate;
            updatedMandate.Name = "Updated name";
            var updateResult = await mandateController.Edit(updatedMandate).ConfigureAwait(false);
            ActionResultAssert.IsOk(updateResult);
            var resultValue = (updateResult as OkObjectResult)?.Value as Mandate;
            Assert.AreEqual(updatedMandate.Name, resultValue?.Name);
        }

        [TestMethod]
        public async Task EditMandateUnauthorized()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate() { FileTypes = new string[] { ".*" }, Name = "Test update" };
            var result = await mandateController.Create(mandate).ConfigureAwait(false) as CreatedResult;

            mandateController.SetupTestUser(editUser);
            var updatedMandate = result?.Value as Mandate;
            updatedMandate.Name = "Updated name";
            var updateResult = await mandateController.Edit(updatedMandate).ConfigureAwait(false);
            ActionResultAssert.IsUnauthorized(updateResult);
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
