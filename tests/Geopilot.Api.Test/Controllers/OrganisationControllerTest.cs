using Geopilot.Api.Controllers;
using Geopilot.Api.DTOs;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Controllers
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

            editUser = new User { AuthIdentifier = "123", FullName = "Edit User" };
            context.Users.Add(editUser);
            adminUser = new User { AuthIdentifier = "1234", FullName = "Admin User", IsAdmin = true };
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

            var expectedDto = OrganisationDto.FromOrganisation(testOrganisation);
            Assert.IsNotNull(organisations);
            Assert.AreEqual(4, organisations.Count);
            ContainsOrganisation(organisations, expectedDto);
        }

        [TestMethod]
        public async Task CreateOrganisation()
        {
            organisationController.SetupTestUser(adminUser);
            var organisation = new OrganisationDto
            {
                Name = "NewOrg",
                Users = new List<int> { editUser.Id },
                Mandates = new List<int> { unrestrictedMandate.Id },
            };
            var result = await organisationController.Create(organisation).ConfigureAwait(false);
            ActionResultAssert.IsCreated(result);
            var resultValue = (result as CreatedResult)?.Value as OrganisationDto;
            Assert.IsNotNull(resultValue);

            var organisations = organisationController.Get();
            Assert.IsNotNull(organisations);
            ContainsOrganisation(organisations, resultValue);
        }

        [TestMethod]
        public async Task EditOrganisation()
        {
            organisationController.SetupTestUser(adminUser);
            var organisation = new OrganisationDto
            {
                Name = "NewOrg",
                Users = new List<int> { editUser.Id },
                Mandates = new List<int> { unrestrictedMandate.Id, xtfMandate.Id },
            };
            var result = await organisationController.Create(organisation).ConfigureAwait(false) as CreatedResult;

            var updatedOrganisation = result?.Value as OrganisationDto;
            Assert.IsNotNull(updatedOrganisation);
            updatedOrganisation.Name = "UpdatedOrg";
            updatedOrganisation.Users = new List<int> { adminUser.Id };
            updatedOrganisation.Mandates = new List<int> { xtfMandate.Id, unassociatedMandate.Id };

            var updateResult = await organisationController.Edit(updatedOrganisation).ConfigureAwait(false);
            ActionResultAssert.IsOk(updateResult);
            var resultValue = (updateResult as OkObjectResult)?.Value as OrganisationDto;
            Assert.IsNotNull(resultValue);

            var organisations = organisationController.Get();
            Assert.IsNotNull(organisations);
            ContainsOrganisation(organisations, updatedOrganisation);
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
            loggerMock.VerifyAll();
        }

        private void ContainsOrganisation(IEnumerable<OrganisationDto> organisations, OrganisationDto organisation)
        {
            var found = organisations.FirstOrDefault(m => m.Id == organisation.Id);
            Assert.IsNotNull(found);
            CompareOrganisations(organisation, found);
        }

        private void CompareOrganisations(OrganisationDto expected, OrganisationDto actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Name, actual.Name);
            CollectionAssert.AreEqual(expected.Mandates, actual.Mandates);
            CollectionAssert.AreEqual(expected.Users, actual.Users);
        }
    }
}
