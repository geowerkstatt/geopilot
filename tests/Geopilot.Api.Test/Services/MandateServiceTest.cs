using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline.Config;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class MandateServiceTest
{
    private Context context;
    private MandateService mandateService;
    private UploadStore uploadStore;
    private User editUser;
    private User adminUser;
    private Mandate unrestrictedMandate;
    private Mandate noDeliveryMandate;
    private Mandate xtfMandate;
    private Mandate publicCsvMandate;
    private Mandate noOrganisationsMandate;
    private Mandate noPermissionMandate;
    private Mandate missingPipelineMandate;
    private Mock<IPipelineService> pipelineServiceMock;

    [TestInitialize]
    public void Initialize()
    {
        const string existingPipelineId = "existing-pipeline";
        const string missingPipelineId = "missing-pipeline";

        context = AssemblyInitialize.DbFixture.GetTestContext();
        uploadStore = new UploadStore();

        var pipelineConfig = new PipelineConfig
        {
            Id = existingPipelineId,
            DisplayName = new Dictionary<string, string> { { "en", "Existing Pipeline" } },
            Steps = [],
        };
        pipelineServiceMock = new Mock<IPipelineService>();
        pipelineServiceMock.Setup(s => s.GetAvailablePipelines()).Returns([pipelineConfig]);

        mandateService = new MandateService(context, uploadStore, pipelineServiceMock.Object);

        unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(unrestrictedMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        noDeliveryMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(noDeliveryMandate)), AllowDelivery = false, PipelineId = existingPipelineId };
        xtfMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = TestHelpers.Localized(nameof(xtfMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        publicCsvMandate = new Mandate { FileTypes = new string[] { ".csv" }, Name = TestHelpers.Localized(nameof(publicCsvMandate)), IsPublic = true, AllowDelivery = true, PipelineId = existingPipelineId };
        noOrganisationsMandate = new Mandate { FileTypes = new string[] { ".itf" }, Name = TestHelpers.Localized(nameof(noOrganisationsMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        noPermissionMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(noPermissionMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        missingPipelineMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(missingPipelineMandate)), AllowDelivery = true, PipelineId = missingPipelineId };

        context.Mandates.Add(unrestrictedMandate);
        context.Mandates.Add(noDeliveryMandate);
        context.Mandates.Add(xtfMandate);
        context.Mandates.Add(publicCsvMandate);
        context.Mandates.Add(noOrganisationsMandate);
        context.Mandates.Add(noPermissionMandate);
        context.Mandates.Add(missingPipelineMandate);

        editUser = CreateUser("ms-123", "Edit User", "example@example.org");
        context.Users.Add(editUser);

        adminUser = CreateUser("ms-1234", "Admin User", "admin.example@example.org", isAdmin: true);
        context.Users.Add(adminUser);

        var organisation = new Organisation { Name = "GAMMAHUNT" };
        organisation.Mandates.Add(unrestrictedMandate);
        organisation.Mandates.Add(noDeliveryMandate);
        organisation.Mandates.Add(xtfMandate);
        organisation.Mandates.Add(publicCsvMandate);
        organisation.Mandates.Add(missingPipelineMandate);
        organisation.Users.Add(editUser);
        organisation.Users.Add(adminUser);

        var organisation2 = new Organisation { Name = "DELTALIGHT" };
        organisation2.Mandates.Add(noPermissionMandate);
        organisation2.Users.Add(adminUser);

        context.Add(organisation);
        context.Add(organisation2);
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForAuthenticatedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var publicMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Public Mandate"), IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(publicMandate.Id, user);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForUnauthenticatedUser()
    {
        var publicMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Public Mandate"), IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(publicMandate.Id, null);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonPublicMandateWhenUnauthenticated()
    {
        var privateMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Private Mandate"), IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(privateMandate.Id, null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsMandateForAuthorizedUser()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation();

        var result = await mandateService.GetMandateForUser(mandate.Id, user);

        Assert.IsNotNull(result);
        Assert.AreEqual(mandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForUnauthorizedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var mandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Restricted Mandate"), IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(mandate.Id, user);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonExistentMandate()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(int.MaxValue, user);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandatesAsAdminUser()
    {
        var result = await mandateService.GetMandatesAsync();

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, publicCsvMandate);
        ContainsMandate(result, noOrganisationsMandate);
        ContainsMandate(result, noPermissionMandate);
        ContainsMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsNonAdmin()
    {
        var uploadId = CreateUpload("Original.xtf");

        var result = await mandateService.GetMandateSummariesAsync(editUser, uploadId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsAdmin()
    {
        var uploadId = CreateUpload("Original.xtf");

        var result = await mandateService.GetMandateSummariesAsync(adminUser, uploadId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsUnauthenticated()
    {
        var uploadId = CreateUpload("Original.xtf");

        var result = await mandateService.GetMandateSummariesAsync(null, uploadId);

        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, unrestrictedMandate);
        DoesNotContainMandate(result, noDeliveryMandate);
        DoesNotContainMandate(result, xtfMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdIgnoresCase()
    {
        var uploadId = CreateUpload("Original.XTF");

        var result = await mandateService.GetMandateSummariesAsync(editUser, uploadId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUnknownUploadIdThrows()
    {
        var unknownUploadId = Guid.NewGuid();

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await mandateService.GetMandateSummariesAsync(editUser, unknownUploadId));
    }

    [TestMethod]
    public async Task GetMandateSummariesWithDefaultUploadIdThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await mandateService.GetMandateSummariesAsync(editUser, default));
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadWithoutFileExtensionsThrows()
    {
        var uploadId = CreateUpload("noextension");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await mandateService.GetMandateSummariesAsync(editUser, uploadId));
    }

    private Guid CreateUpload(params string[] fileNames)
    {
        var uploadId = Guid.NewGuid();
        var files = fileNames
            .Select(name => new CloudFileInfo(name, $"blobs/{name}", 1024))
            .ToImmutableList();
        uploadStore.CreateUpload(uploadId, files);
        return uploadId;
    }

    private void ContainsMandate(IEnumerable<Mandate> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNotNull(found, $"mandate with id '{mandate.Id}' and name '{mandate.Name}' not found");
    }

    private void ContainsMandate(IEnumerable<MandateSummary> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNotNull(found, $"mandate with id '{mandate.Id}' and name '{mandate.Name}' not found");
    }

    private void DoesNotContainMandate(IEnumerable<MandateSummary> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNull(found);
    }
}
