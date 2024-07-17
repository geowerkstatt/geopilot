using Geopilot.Api.DTOs;
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
        private Mandate testMandate;
        private Organisation testOrganisation;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<OrganisationController>>();
            context = AssemblyInitialize.DbFixture.GetTestContext();
            organisationController = new OrganisationController(loggerMock.Object, context);

            testMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = nameof(testMandate) };
            context.Mandates.Add(testMandate);

            editUser = new User { AuthIdentifier = "123", FullName = "Edit User" };
            context.Users.Add(editUser);
            adminUser = new User { AuthIdentifier = "1234", FullName = "Admin User", IsAdmin = true };
            context.Users.Add(adminUser);

            testOrganisation = new Organisation { Name = "TestOrg" };
            testOrganisation.Mandates.Add(testMandate);
            testOrganisation.Users.Add(editUser);
            testOrganisation.Users.Add(adminUser);

            context.Add(testOrganisation);
            context.SaveChanges();
        }

        [TestMethod]
        public void GetOrganisations()
        {
            var result = organisationController.Get() as OkObjectResult;
            var organisations = (result?.Value as IEnumerable<OrganisationDto>)?.ToList();

            var expectedDto = OrganisationDto.FromOrganisation(testOrganisation);
            Assert.IsNotNull(organisations);
            Assert.AreEqual(4, organisations.Count);
            ContainsOrganisation(organisations, expectedDto);
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
