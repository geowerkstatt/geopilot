using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using System.Collections.Immutable;

namespace Geopilot.Api.Controllers
{
    [TestClass]
    public class MandateControllerTest
    {
        private Mock<ILogger<MandateController>> loggerMock;
        private Mock<IValidationService> validationServiceMock;
        private Mock<IValidator> interlisValidatorMock;
        private Mock<IPipelineService> pipelineServiceMock;
        private Context context;
        private MandateController mandateController;
        private User editUser;
        private User adminUser;
        private Mandate unrestrictedMandate;
        private Mandate xtfMandate;
        private Mandate publicCsvMandate;
        private Mandate noOrganisationsMandate;
        private Mandate noPermissionMandate;
        private Organisation organisation;
        private Organisation organisation2;

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<MandateController>>();
            validationServiceMock = new Mock<IValidationService>();
            context = AssemblyInitialize.DbFixture.GetTestContext();

            interlisValidatorMock = new Mock<IValidator>();
            var validatorMocks = new List<IValidator> { interlisValidatorMock.Object };
            pipelineServiceMock = new Mock<IPipelineService>();

            mandateController = new MandateController(loggerMock.Object, context, validationServiceMock.Object, validatorMocks, pipelineServiceMock.Object);

            unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(unrestrictedMandate) };
            xtfMandate = new Mandate
            {
                FileTypes = new string[] { ".xtf" },
                Name = nameof(xtfMandate),
                SpatialExtent = Geometry.DefaultFactory.CreatePolygon(new NetTopologySuite.Geometries.Coordinate[]
                {
                    new(8.046284, 47.392423),
                    new(8.057055, 47.392423),
                    new(8.057055, 47.388181),
                    new(8.046284, 47.388181),
                    new(8.046284, 47.392423),
                }),
            };
            xtfMandate.SetCoordinateListFromPolygon();

            publicCsvMandate = new Mandate { FileTypes = new string[] { ".csv" }, Name = nameof(publicCsvMandate), IsPublic = true };
            noOrganisationsMandate = new Mandate { FileTypes = new string[] { ".itf" }, Name = nameof(noOrganisationsMandate) };
            noPermissionMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(noPermissionMandate) };

            context.Mandates.Add(unrestrictedMandate);
            context.Mandates.Add(xtfMandate);
            context.Mandates.Add(publicCsvMandate);
            context.Mandates.Add(noOrganisationsMandate);
            context.Mandates.Add(noPermissionMandate);

            editUser = CreateUser("123", "Edit User", "example@example.org");
            context.Users.Add(editUser);

            adminUser = CreateUser("1234", "Admin User", "admin.example@example.org", isAdmin: true);
            context.Users.Add(adminUser);

            organisation = new Organisation { Name = "GAMMAHUNT" };
            organisation.Mandates.Add(unrestrictedMandate);
            organisation.Mandates.Add(xtfMandate);
            organisation.Mandates.Add(publicCsvMandate);
            organisation.Users.Add(editUser);
            organisation.Users.Add(adminUser);

            organisation2 = new Organisation { Name = "DELTALIGHT" };
            organisation2.Mandates.Add(noPermissionMandate);
            organisation2.Users.Add(adminUser);

            context.Add(organisation);
            context.Add(organisation2);
            context.SaveChanges();
        }

        [TestMethod]
        public async Task GetAsNonAdminUser()
        {
            mandateController.SetupTestUser(editUser);

            var result = (await mandateController.Get()) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            ContainsMandate(mandates, publicCsvMandate);
            DoesNotContainMandate(mandates, noOrganisationsMandate);
            DoesNotContainMandate(mandates, noPermissionMandate);
        }

        [TestMethod]
        public async Task GetAsAdminUser()
        {
            mandateController.SetupTestUser(adminUser);

            var result = (await mandateController.Get()) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            ContainsMandate(mandates, publicCsvMandate);
            ContainsMandate(mandates, noOrganisationsMandate);
            ContainsMandate(mandates, noPermissionMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdAsNonAdmin()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(editUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, publicCsvMandate);
            DoesNotContainMandate(mandates, noOrganisationsMandate);
            DoesNotContainMandate(mandates, noPermissionMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdAsAdmin()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(adminUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            ContainsMandate(mandates, noPermissionMandate);
            DoesNotContainMandate(mandates, noOrganisationsMandate);
            DoesNotContainMandate(mandates, publicCsvMandate);
        }

        [TestMethod]
        public async Task GetAsUnauthenticated()
        {
            var result = (await mandateController.Get()) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, publicCsvMandate);
            DoesNotContainMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, unrestrictedMandate);
            DoesNotContainMandate(mandates, noOrganisationsMandate);
            DoesNotContainMandate(mandates, noPermissionMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdAsUnauthenticated()
        {
            var jobId = Guid.NewGuid();
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            DoesNotContainMandate(mandates, publicCsvMandate);
            DoesNotContainMandate(mandates, unrestrictedMandate);
            DoesNotContainMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, noOrganisationsMandate);
            DoesNotContainMandate(mandates, noPermissionMandate);
        }

        [TestMethod]
        public async Task GetWithJobIdIgnoresCase()
        {
            var jobId = Guid.NewGuid();
            mandateController.SetupTestUser(editUser);
            validationServiceMock
                .Setup(m => m.GetJob(jobId))
                .Returns(new ValidationJob(jobId, "Original.XTF", "tmp.XTF", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

            var result = (await mandateController.Get(jobId)) as OkObjectResult;
            var mandates = (result?.Value as IEnumerable<Mandate>)?.ToList();

            Assert.IsNotNull(mandates);
            ContainsMandate(mandates, unrestrictedMandate);
            ContainsMandate(mandates, xtfMandate);
            DoesNotContainMandate(mandates, publicCsvMandate);
            DoesNotContainMandate(mandates, noOrganisationsMandate);
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
            Assert.IsEmpty(mandates);
        }

        [TestMethod]
        public async Task GetWithoutValidDbUserThrowsException()
        {
            mandateController.SetupTestUser(new User { AuthIdentifier = "NotRegisteredUserId" });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await mandateController.Get());
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
        public async Task GetByIdAsync()
        {
            var mandateId = context.Mandates.First().Id;

            var response = await mandateController.GetById(mandateId);
            ActionResultAssert.IsOk(response);
            var mandate = (response as OkObjectResult)?.Value as Mandate;
            Assert.IsNotNull(mandate);
            Assert.AreEqual(mandateId, mandate.Id);
            Assert.AreEqual("Handmade Soft Cheese", mandate.Name);
            Assert.HasCount(2, mandate.Coordinates);
            Assert.HasCount(2, mandate.Deliveries);
            Assert.AreEqual(FieldEvaluationType.Optional, mandate.EvaluatePrecursorDelivery);
            Assert.AreEqual(FieldEvaluationType.Required, mandate.EvaluatePartial);
            Assert.AreEqual(FieldEvaluationType.NotEvaluated, mandate.EvaluateComment);
            Assert.HasCount(4, mandate.FileTypes);
            Assert.HasCount(1, mandate.Organisations);
        }

        [TestMethod]
        public async Task GetByIdNotFoundAsync()
        {
            var response = await mandateController.GetById(int.MaxValue);
            ActionResultAssert.IsNotFound(response);
        }

        [TestMethod]
        [DataRow("DEFAULT", null, DisplayName = "create mandate with profile")]
        [DataRow(null, "pipeline_id", DisplayName = "create mandate with pipeline")]
        [DataRow("DEFAULT", "pipeline_id", DisplayName = "create mandate with profile and pipeline")]
        [DataRow(null, null, DisplayName = "create mandate with profile and pipeline undefined")]
        public async Task CreateMandate(string profile, string pipelineId)
        {
            interlisValidatorMock.Setup(v => v.GetSupportedProfilesAsync())
                .ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });
            if (pipelineId != null)
            {
                var pipelineStub = new PipelineConfig()
                {
                    Id = "Pipeline1",
                    DisplayName = new Dictionary<string, string>()
                {
                    { "en", "pipeline 1" },
                    { "de", "Pipeline 1" },
                },
                    Parameters = new PipelineParametersConfig() { UploadStep = "", Mappings = new List<FileMappingsConfig>(), },
                    Steps = new List<StepConfig>(),
                };
                pipelineServiceMock.Setup(v => v.GetById(pipelineId)).Returns(pipelineStub);
            }

            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*" },
                Name = "ACCORDIANWALK",
                Organisations = new List<Organisation> { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate> { new() { X = 7.93770851245525, Y = 46.706944924654366 }, new() { X = 8.865921640681403, Y = 47.02476048042957 } },
                InterlisValidationProfile = profile,
                PipelineId = pipelineId,
            };

            var result = await mandateController.Create(mandate);
            ActionResultAssert.IsCreated(result);
            var resultValue = (result as CreatedResult)?.Value as Mandate;
            Assert.IsNotNull(resultValue);
            CompareMandates(mandate, resultValue);
        }

        [TestMethod]
        [DataRow("NONEXISTING", DisplayName = "CreateMandateWithNonExistentProfile")]
        [DataRow("", DisplayName = "CreateMandateWithProfileEmptyString")]
        public async Task CreateMandateChecksProfileExists(string profile)
        {
            interlisValidatorMock.Setup(v => v.GetSupportedProfilesAsync())
                .ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*" },
                Name = "ACCORDIANWALK",
                Organisations = new List<Organisation> { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate> { new() { X = 7.93770851245525, Y = 46.706944924654366 }, new() { X = 8.865921640681403, Y = 47.02476048042957 } },
                InterlisValidationProfile = profile,
            };
            var result = await mandateController.Create(mandate);
            ActionResultAssert.IsBadRequest(result);
        }

        [TestMethod(DisplayName = "create mandate checks pipeline exists")]
        [DataRow("NONEXISTING", DisplayName = "create mandate with non existent pipeline")]
        [DataRow("", DisplayName = "create mandate with pipeline empty string")]
        public async Task CreateMandateChecksPipelineExists(string pipelineId)
        {
            pipelineServiceMock.Setup(v => v.GetById(pipelineId))
                .Throws(new KeyNotFoundException($"Pipeline with ID '{pipelineId}' not found."));
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*" },
                Name = "ACCORDIANWALK",
                Organisations = new List<Organisation> { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate> { new() { X = 7.93770851245525, Y = 46.706944924654366 }, new() { X = 8.865921640681403, Y = 47.02476048042957 } },
                PipelineId = pipelineId,
            };
            var result = await mandateController.Create(mandate);
            ActionResultAssert.IsBadRequest(result);
        }

        [TestMethod]
        public async Task CreateMandateRequiresSpatialExtent()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*" },
                Name = "ACCORDIANWALK",
                Organisations = new List<Organisation> { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate>(),
            };

            var result = await mandateController.Create(mandate);
            ActionResultAssert.IsBadRequest(result);
        }

        [TestMethod]
        [DataRow("different-profile", null, DisplayName = "edit mandate with profile")]
        [DataRow(null, "pipeline_id", DisplayName = "edit mandate with pipeline")]
        [DataRow("different-profile", "pipeline_id", DisplayName = "edit mandate with profile and pipeline")]
        [DataRow(null, null, DisplayName = "edit mandate with profile and pipeline null")]
        public async Task EditMandate(string newProfile, string pipelineId)
        {
            interlisValidatorMock.Setup(v => v.GetSupportedProfilesAsync())
                .ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" }, new Profile { Id = "different-profile" } });
            if (pipelineId != null)
            {
                var pipelineStub = new PipelineConfig()
                {
                    Id = "Pipeline1",
                    DisplayName = new Dictionary<string, string>()
                {
                    { "en", "pipeline 1" },
                    { "de", "Pipeline 1" },
                },
                    Parameters = new PipelineParametersConfig() { UploadStep = "", Mappings = new List<FileMappingsConfig>(), },
                    Steps = new List<StepConfig>(),
                };
                pipelineServiceMock.Setup(v => v.GetById(pipelineId)).Returns(pipelineStub);
            }

            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                FileTypes = new string[] { ".*", ".zip" },
                Name = "PEARLFOLLOWER",
                InterlisValidationProfile = "DEFAULT",
                Organisations = new List<Organisation> { new() { Id = 1 }, new() { Id = organisation.Id } },
                Coordinates = new List<Models.Coordinate> { new() { X = 7.93770851245525, Y = 46.706944924654366 }, new() { X = 8.865921640681403, Y = 47.02476048042957 } },
            };

            var mandateToUpdateResult = await mandateController.Create(mandate) as CreatedResult;
            var mandateToUpdate = mandateToUpdateResult?.Value as Mandate;
            Assert.IsNotNull(mandateToUpdate);

            var guid = Guid.NewGuid();
            validationServiceMock
                .Setup(s => s.GetJob(guid))
                .Returns(new ValidationJob(guid, "ORIGINAL.zip", "TEMP.zip", mandateToUpdate.Id, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Completed, DateTime.Now));
            var assetHandlerMock = new Mock<IAssetHandler>();
            assetHandlerMock
                .Setup(p => p.PersistJobAssets(guid))
                .Returns(new List<Asset> { new Asset(), new Asset() });

            var deliveryController = new DeliveryController(new Mock<ILogger<DeliveryController>>().Object, context, validationServiceMock.Object, assetHandlerMock.Object);
            deliveryController.SetupTestUser(editUser);

            var request = new DeliveryRequest
            {
                JobId = guid,
            };

            var result = await deliveryController.Create(request);
            var delivery = (result as CreatedResult)?.Value as Delivery;
            Assert.IsNotNull(delivery);

            mandateToUpdate.Name = "ARKMUTANT";
            mandateToUpdate.InterlisValidationProfile = newProfile;
            mandateToUpdate.PipelineId = pipelineId;
            mandateToUpdate.FileTypes = new string[] { ".zip", ".gpkg" };
            mandateToUpdate.Organisations = new List<Organisation> { new() { Id = 3 }, new() { Id = organisation.Id } };
            mandateToUpdate.Coordinates = new List<Models.Coordinate> { new() { X = 7.93, Y = 46.70 }, new() { X = 8.86, Y = 47.02 } };
            mandateToUpdate.Deliveries = new List<Delivery>();

            var updateResult = await mandateController.Edit(mandateToUpdate);
            var updatedMandate = ActionResultAssert.IsOkObjectResult<Mandate>(updateResult);
            Assert.IsNotNull(updatedMandate);

            Assert.HasCount(1, updatedMandate.Deliveries);
            Assert.AreEqual(delivery.Id, updatedMandate.Deliveries[0].Id);
            Assert.AreEqual(mandateToUpdate.Name, updatedMandate.Name);
            Assert.AreEqual(mandateToUpdate.InterlisValidationProfile, updatedMandate.InterlisValidationProfile);
            Assert.AreEqual(mandateToUpdate.PipelineId, updatedMandate.PipelineId);
            CollectionAssert.AreEqual(mandateToUpdate.FileTypes, updatedMandate.FileTypes);
            CollectionAssert.AreEqual(mandateToUpdate.Coordinates, updatedMandate.Coordinates);
            Assert.HasCount(mandateToUpdate.Organisations.Count, updatedMandate.Organisations);
            for (var i = 0; i < mandateToUpdate.Organisations.Count; i++)
            {
                Assert.AreEqual(mandateToUpdate.Organisations[i].Id, updatedMandate.Organisations[i].Id);
            }
        }

        [TestMethod]
        public async Task EditMandateRequiresSpatialExtent()
        {
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                Id = xtfMandate.Id,
                FileTypes = new string[] { ".*", ".zip" },
                Name = "PEARLFOLLOWER",
                Organisations = new List<Organisation>() { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate>(),
            };
            var result = await mandateController.Edit(mandate);
            ActionResultAssert.IsBadRequest(result);
        }

        [TestMethod]
        [DataRow("NONEXISTING", DisplayName = "edit mandate with non existent profile")]
        [DataRow("", DisplayName = "edit mandate with profile empty string")]
        public async Task EditMandateChecksProfileExists(string profile)
        {
            interlisValidatorMock.Setup(v => v.GetSupportedProfilesAsync())
                .ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                Id = xtfMandate.Id,
                FileTypes = new string[] { ".*", ".zip" },
                Name = "PEARLFOLLOWER",
                Organisations = new List<Organisation>() { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate> { new() { X = 7.93770851245525, Y = 46.706944924654366 }, new() { X = 8.865921640681403, Y = 47.02476048042957 } },
                InterlisValidationProfile = profile,
            };
            var result = await mandateController.Edit(mandate);
            ActionResultAssert.IsBadRequest(result);
        }

        [TestMethod]
        [DataRow("NONEXISTING", DisplayName = "edit mandate with non existent pipeline")]
        [DataRow("", DisplayName = "edit mandate with pipeline empty string")]
        public async Task EditMandateChecksPipelineExists(string pipelineId)
        {
            interlisValidatorMock.Setup(v => v.GetSupportedProfilesAsync())
                .ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });
            mandateController.SetupTestUser(adminUser);
            var mandate = new Mandate()
            {
                Id = xtfMandate.Id,
                FileTypes = new string[] { ".*", ".zip" },
                Name = "PEARLFOLLOWER",
                Organisations = new List<Organisation>() { new() { Id = 1 } },
                Coordinates = new List<Models.Coordinate> { new() { X = 7.93770851245525, Y = 46.706944924654366 }, new() { X = 8.865921640681403, Y = 47.02476048042957 } },
                PipelineId = pipelineId,
            };
            var result = await mandateController.Edit(mandate);
            ActionResultAssert.IsBadRequest(result);
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
            loggerMock.VerifyAll();
            validationServiceMock.VerifyAll();
            pipelineServiceMock.VerifyAll();
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
            Assert.AreEqual(expected.IsPublic, actual.IsPublic);
            Assert.AreEqual(expected.InterlisValidationProfile, actual.InterlisValidationProfile);
            CollectionAssert.AreEqual(expected.FileTypes, actual.FileTypes);
            CollectionAssert.AreEqual(expected.Deliveries, actual.Deliveries);
            CollectionAssert.AreEqual(expected.Coordinates, actual.Coordinates);
            Assert.HasCount(expected.Organisations.Count, actual.Organisations);
            for (var i = 0; i < expected.Organisations.Count; i++)
            {
                Assert.AreEqual(expected.Organisations[i].Id, actual.Organisations[i].Id);
                Assert.AreEqual(expected.Organisations[i].Name, actual.Organisations[i].Name);
            }
        }
    }
}
