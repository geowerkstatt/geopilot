using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;

namespace Geopilot.Api.Controllers
{
    [TestClass]
    public class MandateControllerTest
    {
        private Mock<ILogger<MandateController>> loggerMock;
        private Mock<IValidationService> validationServiceMock;
        private Context context;
        private MandateController mandateController;
        private User editUser;
        private User adminUser;
        private Mandate unrestrictedMandate;
        private Mandate xtfMandate;
        private Mandate unassociatedMandate;
        private Organisation organisation;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<MandateController>>();
            validationServiceMock = new Mock<IValidationService>();
            context = AssemblyInitialize.DbFixture.GetTestContext();
            mandateController = new MandateController(loggerMock.Object, context, validationServiceMock.Object);

            unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(unrestrictedMandate) };
            xtfMandate = new Mandate
            {
                FileTypes = new string[] { ".xtf" },
                Name = nameof(xtfMandate),
                SpatialExtent = Geometry.DefaultFactory.CreatePolygon(new NetTopologySuite.Geometries.Coordinate[]
                {
                    new (8.046284, 47.392423),
                    new (8.057055, 47.392423),
                    new (8.057055, 47.388181),
                    new (8.046284, 47.388181),
                    new (8.046284, 47.392423),
                }),
            };
            unassociatedMandate = new Mandate { FileTypes = new string[] { "*.itf" }, Name = nameof(unassociatedMandate) };

            context.Mandates.Add(unrestrictedMandate);
            context.Mandates.Add(xtfMandate);
            context.Mandates.Add(unassociatedMandate);

            editUser = CreateUser("123", "Edit User", "example@example.org");
            context.Users.Add(editUser);

            adminUser = CreateUser("1234", "Admin User", "admin.example@example.org", isAdmin: true);
            context.Users.Add(adminUser);

            organisation = new Organisation { Name = "GAMMAHUNT" };
            organisation.Mandates.Add(unrestrictedMandate);
            organisation.Mandates.Add(xtfMandate);
            organisation.Users.Add(editUser);
            organisation.Users.Add(adminUser);

            context.Add(organisation);
            context.SaveChanges();
        }

        [TestMethod]
        public async Task GetReturnsListOfMandatesForUser()
        {
            mandateController.SetupTestUser(editUser);
            xtfMandate.SetCoordinateListFromPolygon();

            var result = (await mandateController.Get()) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, unassociatedMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdIncludesMatchingMandates()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(editUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf"));
            xtfMandate.SetCoordinateListFromPolygon();

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, unassociatedMandate);
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
            ContainsMandate(mandates, unrestrictedMandate);
            DoesNotContainMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, unassociatedMandate);
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
            Assert.AreEqual(0, mandates.Count);
        }

        [TestMethod]
        public async Task GetWithoutValidDbUserThrowsException()
        {
            mandateController.SetupTestUser(new User { AuthIdentifier = "NotRegisteredUserId" });
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await mandateController.Get());
        }

        [TestMethod]
        public async Task GetExtractsCorrectCoordinates()
        {
            mandateController.SetupTestUser(adminUser);
            var result = await mandateController.Get() as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();
            Assert.IsNotNull(mandates);
            var xtfMandateDto = mandates.FirstOrDefault(m => m.Id == xtfMandate.Id);
            Assert.IsNotNull(xtfMandateDto);
            Assert.AreEqual(8.046284, xtfMandateDto.Coordinates[0].X);
            Assert.AreEqual(47.388181, xtfMandateDto.Coordinates[0].Y);
            Assert.AreEqual(8.057055, xtfMandateDto.Coordinates[1].X);
            Assert.AreEqual(47.392423, xtfMandateDto.Coordinates[1].Y);
        }

        [TestMethod]
        public async Task CreateMandate()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*" },
                Name = "ACCORDIANWALK",
                Organisations = new List<Organisation>() { new () { Id = 1 } },
                Coordinates = new List<Models.Coordinate>() { new () { X = 7.93770851245525, Y = 46.706944924654366 }, new () { X = 8.865921640681403, Y = 47.02476048042957 } },
            };
            var result = await mandateController.Create(mandate);
            ActionResultAssert.IsCreated(result);
            var resultValue = (result as CreatedResult)?.Value as Mandate;
            Assert.IsNotNull(resultValue);
            Assert.AreEqual(mandate.Name, resultValue.Name);
            CollectionAssert.AreEqual(mandate.FileTypes, resultValue.FileTypes);
            Assert.AreEqual(mandate.Organisations.Count, resultValue.Organisations.Count);
            Assert.AreEqual(mandate.Organisations[0].Id, resultValue.Organisations[0].Id);
            Assert.IsNotNull(resultValue.Organisations[0].Name);
            CollectionAssert.AreEqual(mandate.Coordinates, resultValue.Coordinates);
        }

        [TestMethod]
        public async Task EditMandate()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*", ".zip" },
                Name = "PEARLFOLLOWER",
                Organisations = new List<Organisation>() { new () { Id = 1 }, new () { Id = organisation.Id } },
                Coordinates = new List<Models.Coordinate> { new () { X = 7.93770851245525, Y = 46.706944924654366 }, new () { X = 8.865921640681403, Y = 47.02476048042957 } },
            };
            var mandateToUpdateResult = await mandateController.Create(mandate) as CreatedResult;
            var mandateToUpdate = mandateToUpdateResult?.Value as Mandate;
            Assert.IsNotNull(mandateToUpdate);

            var guid = Guid.NewGuid();
            validationServiceMock
                .Setup(s => s.GetJobStatus(guid))
                .Returns(new ValidationJobStatus(guid) { JobId = guid, Status = Status.Completed });
            var assetHandlerMock = new Mock<IAssetHandler>();
            assetHandlerMock
                .Setup(p => p.PersistJobAssets(guid))
                .Returns(new List<Asset> { new Asset(), new Asset() });

            var deliveryController = new DeliveryController(new Mock<ILogger<DeliveryController>>().Object, context, validationServiceMock.Object, assetHandlerMock.Object);
            deliveryController.SetupTestUser(editUser);

            var request = new DeliveryRequest
            {
                JobId = guid,
                MandateId = mandateToUpdate.Id,
                PartialDelivery = false,
            };

            var result = await deliveryController.Create(request);
            var delivery = (result as CreatedResult)?.Value as Delivery;
            Assert.IsNotNull(delivery);

            mandateToUpdate.Name = "ARKMUTANT";
            mandateToUpdate.FileTypes = new string[] { ".zip", ".gpkg" };
            mandateToUpdate.Organisations = new List<Organisation>() { new () { Id = 3 }, new () { Id = organisation.Id } };
            mandateToUpdate.Coordinates = new List<Models.Coordinate> { new () { X = 7.93, Y = 46.70 }, new () { X = 8.86, Y = 47.02 } };
            mandateToUpdate.Deliveries = new List<Delivery>();

            var updateResult = await mandateController.Edit(mandateToUpdate);
            var updatedMandate = ActionResultAssert.IsOkObjectResult<Mandate>(updateResult);
            Assert.IsNotNull(updatedMandate);

            Assert.AreEqual(mandateToUpdate.Name, updatedMandate.Name);
            CollectionAssert.AreEqual(mandateToUpdate.FileTypes, updatedMandate.FileTypes);
            Assert.AreEqual(1, updatedMandate.Deliveries.Count);
            Assert.AreEqual(delivery.Id, updatedMandate.Deliveries[0].Id);
            CollectionAssert.AreEqual(mandateToUpdate.Coordinates, updatedMandate.Coordinates);
            Assert.AreEqual(mandateToUpdate.Organisations.Count, updatedMandate.Organisations.Count);
            for (var i = 0; i < mandateToUpdate.Organisations.Count; i++)
            {
                Assert.AreEqual(mandateToUpdate.Organisations[i].Id, updatedMandate.Organisations[i].Id);
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
            loggerMock.VerifyAll();
            validationServiceMock.VerifyAll();
        }

        private void ContainsMandate(IEnumerable<Mandate> mandates, Mandate mandate)
        {
            var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
            Assert.IsNotNull(found);
            CompareMandates(mandate, found);
        }

        private void DoesNotContainMandate(IEnumerable<Mandate> mandates, Mandate mandate)
        {
            var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
            Assert.IsNull(found);
        }

        private void CompareMandates(Mandate expected, Mandate actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Name, actual.Name);
            CollectionAssert.AreEqual(expected.FileTypes, actual.FileTypes);
            CollectionAssert.AreEqual(expected.Deliveries, actual.Deliveries);
            CollectionAssert.AreEqual(expected.Coordinates, actual.Coordinates);
            Assert.AreEqual(expected.Organisations.Count, actual.Organisations.Count);
            for (var i = 0; i < expected.Organisations.Count; i++)
            {
                Assert.AreEqual(expected.Organisations[i].Id, actual.Organisations[i].Id);
            }
        }
    }
}
