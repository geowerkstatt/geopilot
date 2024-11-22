using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers
{
    [TestClass]
    public class OrganisationControllerTest
    {
        private Mock<ILogger<OrganisationController>> loggerMock;
        private Context context;
        private OrganisationController organisationController;
        private User editUser;
        private User adminUser;
        private Mandate unrestrictedMandate;
        private Mandate xtfMandate;
        private Mandate unassociatedMandate;
        private Organisation testOrganisation;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<OrganisationController>>();
            context = AssemblyInitialize.DbFixture.GetTestContext();
            organisationController = new OrganisationController(loggerMock.Object, context);

            unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(unrestrictedMandate) };
            xtfMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = nameof(xtfMandate) };
            unassociatedMandate = new Mandate { FileTypes = new string[] { "*.itf" }, Name = nameof(unassociatedMandate) };

            context.Mandates.Add(unrestrictedMandate);
            context.Mandates.Add(xtfMandate);
            context.Mandates.Add(unassociatedMandate);

            editUser = CreateUser("123", "Edit User", "example@example.org");
            context.Users.Add(editUser);

            adminUser = CreateUser("1234", "Admin User", "admin.example@example.org", isAdmin: true);
            context.Users.Add(adminUser);

            testOrganisation = new Organisation { Name = "TestOrg" };
            testOrganisation.Mandates.Add(unrestrictedMandate);
            testOrganisation.Users.Add(editUser);
            testOrganisation.Users.Add(adminUser);

            context.Add(testOrganisation);
            context.SaveChanges();
        }

        [TestMethod]
        public void GetOrganisations()
        {
            var organisations = organisationController.Get();

            Assert.IsNotNull(organisations);
            Assert.AreEqual(4, organisations.Count);
            ContainsOrganisation(organisations, testOrganisation);
        }

        [TestMethod]
        public async Task CreateOrganisation()
        {
            organisationController.SetupTestUser(adminUser);
            var organisation = new Organisation
            {
                Name = "NewOrg",
                Users = new List<User> { new() { Id = editUser.Id } },
                Mandates = new List<Mandate> { new() { Id = unrestrictedMandate.Id } },
            };
            var result = await organisationController.Create(organisation);
            ActionResultAssert.IsCreated(result);
            var resultValue = (result as CreatedResult)?.Value as Organisation;
            Assert.IsNotNull(resultValue);
            Assert.AreEqual(organisation.Name, resultValue.Name);
            Assert.AreEqual(organisation.Users.Count, resultValue.Users.Count);
            Assert.AreEqual(editUser.Id, resultValue.Users[0].Id);
            Assert.AreEqual(organisation.Mandates.Count, resultValue.Mandates.Count);
            Assert.AreEqual(unrestrictedMandate.Id, resultValue.Mandates[0].Id);
        }

        [TestMethod]
        public async Task EditOrganisation()
        {
            organisationController.SetupTestUser(adminUser);
            var organisation = new Organisation
            {
                Name = "NewOrg",
                Users = new List<User> { new() { Id = editUser.Id } },
                Mandates = new List<Mandate> { new() { Id = unrestrictedMandate.Id }, new() { Id = xtfMandate.Id } },
            };
            var result = await organisationController.Create(organisation) as CreatedResult;

            var organisationToUpdate = result?.Value as Organisation;
            Assert.IsNotNull(organisationToUpdate);
            organisationToUpdate.Name = "UpdatedOrg";
            organisationToUpdate.Users = new List<User> { new() { Id = adminUser.Id } };
            organisationToUpdate.Mandates = new List<Mandate> { new() { Id = xtfMandate.Id }, new() { Id = unassociatedMandate.Id } };

            var updateResult = await organisationController.Edit(organisationToUpdate);
            ActionResultAssert.IsOk(updateResult);
            var updatedOrganisation = (updateResult as OkObjectResult)?.Value as Organisation;
            Assert.IsNotNull(updatedOrganisation);
            CompareOrganisations(organisationToUpdate, updatedOrganisation);
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
            loggerMock.VerifyAll();
        }

        private void ContainsOrganisation(IEnumerable<Organisation> organisations, Organisation organisation)
        {
            var found = organisations.FirstOrDefault(m => m.Id == organisation.Id);
            Assert.IsNotNull(found);
            CompareOrganisations(organisation, found);
        }

        private void CompareOrganisations(Organisation expected, Organisation actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Mandates.Count, actual.Mandates.Count);
            for (var i = 0; i < expected.Mandates.Count; i++)
            {
                Assert.AreEqual(expected.Mandates[i].Id, actual.Mandates[i].Id);
            }

            Assert.AreEqual(expected.Users.Count, actual.Users.Count);
            for (var i = 0; i < expected.Users.Count; i++)
            {
                Assert.AreEqual(expected.Users[i].Id, actual.Users[i].Id);
            }
        }
    }
}
