using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private MachineClient machineClient;
        private MachineClient otherMachineClient;
        private Mandate unrestrictedMandate;
        private Mandate xtfMandate;
        private Mandate unassociatedMandate;
        private Organisation testOrganisation;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<OrganisationController>>();
            context = AssemblyInitialize.DbFixture.GetTestContext();
            organisationController = CreateController(machineDeliveryEnabled: true);

            unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(unrestrictedMandate)) };
            xtfMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = TestHelpers.Localized(nameof(xtfMandate)) };
            unassociatedMandate = new Mandate { FileTypes = new string[] { "*.itf" }, Name = TestHelpers.Localized(nameof(unassociatedMandate)) };

            context.Mandates.Add(unrestrictedMandate);
            context.Mandates.Add(xtfMandate);
            context.Mandates.Add(unassociatedMandate);

            editUser = CreateUser("123", "Edit User", "example@example.org");
            context.Users.Add(editUser);

            adminUser = CreateUser("1234", "Admin User", "admin.example@example.org", isAdmin: true);
            context.Users.Add(adminUser);

            machineClient = new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" };
            context.MachineClients.Add(machineClient);

            otherMachineClient = new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "QUIETMEADOW" };
            context.MachineClients.Add(otherMachineClient);

            testOrganisation = new Organisation { Name = "TestOrg" };
            testOrganisation.Mandates.Add(unrestrictedMandate);
            testOrganisation.Users.Add(editUser);
            testOrganisation.Users.Add(adminUser);
            testOrganisation.MachineClients.Add(machineClient);

            context.Add(testOrganisation);
            context.SaveChanges();
        }

        private OrganisationController CreateController(bool machineDeliveryEnabled)
            => new(loggerMock.Object, context, Options.Create(new MachineDeliveryOptions { Enabled = machineDeliveryEnabled }));

        [TestMethod]
        public void GetOrganisations()
        {
            var organisations = organisationController.Get();

            Assert.IsNotNull(organisations);
            Assert.HasCount(4, organisations);
            ContainsOrganisation(organisations, testOrganisation);
        }

        [TestMethod]
        public async Task GetByIdAsync()
        {
            var organisationId = context.Organisations.First().Id;

            var response = await organisationController.GetById(organisationId);
            ActionResultAssert.IsOk(response);
            var organisation = (response as OkObjectResult)?.Value as Organisation;
            Assert.IsNotNull(organisation);
            Assert.AreEqual(organisationId, organisation.Id);
            Assert.AreEqual("Schumm, Runte and Macejkovic", organisation.Name);
            Assert.HasCount(4, organisation.Mandates);
            Assert.HasCount(2, organisation.Users);
        }

        [TestMethod]
        public async Task GetByIdNotFoundAsync()
        {
            var response = await organisationController.GetById(int.MaxValue);
            ActionResultAssert.IsNotFound(response);
        }

        [TestMethod]
        public async Task CreateOrganisation()
        {
            organisationController.SetupTestUser(adminUser);
            var organisation = new Organisation
            {
                Name = "NewOrg",
                Users = new List<User> { new() { Id = editUser.Id } },
                MachineClients = new List<MachineClient> { new() { Id = machineClient.Id } },
                Mandates = new List<Mandate> { new() { Id = unrestrictedMandate.Id } },
            };
            var result = await organisationController.Create(organisation);
            ActionResultAssert.IsCreated(result);
            var resultValue = (result as CreatedResult)?.Value as Organisation;
            Assert.IsNotNull(resultValue);
            Assert.AreEqual(organisation.Name, resultValue.Name);
            Assert.HasCount(organisation.Users.Count, resultValue.Users);
            Assert.AreEqual(editUser.Id, resultValue.Users[0].Id);
            Assert.HasCount(organisation.MachineClients.Count, resultValue.MachineClients);
            Assert.AreEqual(machineClient.Id, resultValue.MachineClients[0].Id);
            Assert.HasCount(organisation.Mandates.Count, resultValue.Mandates);
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
                MachineClients = new List<MachineClient> { new() { Id = machineClient.Id } },
                Mandates = new List<Mandate> { new() { Id = unrestrictedMandate.Id }, new() { Id = xtfMandate.Id } },
            };
            var result = await organisationController.Create(organisation) as CreatedResult;

            var organisationToUpdate = result?.Value as Organisation;
            Assert.IsNotNull(organisationToUpdate);
            organisationToUpdate.Name = "UpdatedOrg";
            organisationToUpdate.Users = new List<User> { new() { Id = adminUser.Id } };
            organisationToUpdate.MachineClients = new List<MachineClient> { new() { Id = otherMachineClient.Id } };
            organisationToUpdate.Mandates = new List<Mandate> { new() { Id = xtfMandate.Id }, new() { Id = unassociatedMandate.Id } };

            var updateResult = await organisationController.Edit(organisationToUpdate);
            ActionResultAssert.IsOk(updateResult);
            var updatedOrganisation = (updateResult as OkObjectResult)?.Value as Organisation;
            Assert.IsNotNull(updatedOrganisation);
            CompareOrganisations(organisationToUpdate, updatedOrganisation);
        }

        [TestMethod]
        public async Task EditKeepsMachineClientsWhenMachineDeliveryDisabled()
        {
            var controller = CreateController(machineDeliveryEnabled: false);
            controller.SetupTestUser(adminUser);

            // The portal of such an installation does not render the clients, so its payload carries none.
            var updateResult = await controller.Edit(new Organisation
            {
                Id = testOrganisation.Id,
                Name = "UpdatedOrg",
                Users = new List<User> { new() { Id = editUser.Id } },
                Mandates = new List<Mandate> { new() { Id = unrestrictedMandate.Id } },
            });

            var updatedOrganisation = ActionResultAssert.IsOkObjectResult<Organisation>(updateResult);
            Assert.HasCount(1, updatedOrganisation.MachineClients, "A payload without clients must not detach the stored ones while nothing can put them back.");
            Assert.AreEqual(machineClient.Id, updatedOrganisation.MachineClients[0].Id);
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
            Assert.HasCount(expected.Mandates.Count, actual.Mandates);
            for (var i = 0; i < expected.Mandates.Count; i++)
            {
                Assert.AreEqual(expected.Mandates[i].Id, actual.Mandates[i].Id);
            }

            Assert.HasCount(expected.Users.Count, actual.Users);
            for (var i = 0; i < expected.Users.Count; i++)
            {
                Assert.AreEqual(expected.Users[i].Id, actual.Users[i].Id);
            }

            Assert.HasCount(expected.MachineClients.Count, actual.MachineClients);
            for (var i = 0; i < expected.MachineClients.Count; i++)
            {
                Assert.AreEqual(expected.MachineClients[i].Id, actual.MachineClients[i].Id);
            }
        }
    }
}
